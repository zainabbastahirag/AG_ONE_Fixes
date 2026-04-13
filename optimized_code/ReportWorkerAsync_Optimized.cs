    public async Task<SpotResult> ReportWorkerAsync(string jobId, CancellationToken ct = default)
    {
        // ────────────────────────────────────────────────────────────
        // STEP 0: Find the job
        // ────────────────────────────────────────────────────────────
        var job = await _db.SpotJobs.FirstOrDefaultAsync(j =>
            j.JobId.ToString() == jobId && j.JobType == (int)JobType.Report, ct);

        if (job == null) return Fail(404, "Job not found.");
        
        if (job.Status == nameof(JobState.Success))
            return Ok("Report already generated successfully.", new { status = "done" });
            
        if (job.Status == nameof(JobState.Failed))
            return Fail(400, "Job previously failed.");

        if (job.Status != nameof(JobState.PendingQueue) && job.Status != nameof(JobState.Processing))
            return Fail(400, $"Job has invalid status: {job.Status}");

        job.Status = nameof(JobState.Processing);
        job.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            // ────────────────────────────────────────────────────────
            // STEP 1: Load all company files from SQL (metadata only)
            // ────────────────────────────────────────────────────────
            await LogStep(jobId, "Step 1/6: Loading company files...", ct);

            var files = await _db.SpotDocuments
                .Where(d => d.CompanyId == job.CompanyId && !d.IsDeleted)
                .ToListAsync(ct);

            if (files.Count == 0)
            {
                await FailReportAsync(jobId, job.CompanyId, "No company files found.", ct);
                return Fail(400, "No company files found.");
            }

            _log.LogInformation("[REPORT] Step 1: Found {Count} files for {Company}", files.Count, job.CompanyId);

            // ────────────────────────────────────────────────────────
            // STEP 2: Validate company documents (SSM cross-check)
            // ────────────────────────────────────────────────────────
            await LogStep(jobId, "Step 2/6: Validating documents...", ct);
            _log.LogInformation("[REPORT] Step 2: Validating documents for {Company}", job.CompanyId);

            var validation = await ValidateCompanyDocumentsAsync(job.CompanyId, ct);
            if (validation.StatusCode != 200)
            {
                _log.LogWarning("[REPORT] Step 2: Company mismatch detected for {Company}, proceeding anyway. Message: {Msg}", job.CompanyId, validation.Message);
                await LogStep(jobId, $"Warning: {validation.Message} — proceeding with report generation.", ct);
            }

            // ────────────────────────────────────────────────────────
            // STEP 3: Extract text from each PDF (Azure Doc Intelligence)
            //         Store extracted text in BLOB STORAGE (not SQL).
            //         Cache: if blob already exists for this file, read
            //         from blob instead of calling Azure again.
            // ────────────────────────────────────────────────────────
            await LogStep(jobId, "Step 3/6: Extracting text from PDFs...", ct);
            _log.LogInformation("[REPORT] Step 3: Extracting/Cleaning text for {Count} files", files.Count);

            var container = _blob.GetBlobContainerClient(_cfg.BlobContainer);
            var extractedDocs = new List<ExtractedDocument>();

            foreach (var file in files)
            {
                var extractedBlobPath = $"extracted-text/{job.CompanyId}/{file.FileUID}.json";
                var extractedBlobClient = container.GetBlobClient(extractedBlobPath);

                // ── CACHE HIT: blob already has the extracted text ──
                if (await extractedBlobClient.ExistsAsync(ct))
                {
                    var cached = await extractedBlobClient.DownloadContentAsync(ct);
                    var cachedText = cached.Value.Content.ToString();

                    extractedDocs.Add(new ExtractedDocument
                    {
                        FileUID = file.FileUID,
                        FileName = file.FileName ?? "unknown",
                        DocType = file.DocumentType ?? "Supplementary",
                        Text = cachedText,
                        PageCount = file.ExtractedPageCount ?? 0
                    });
                    _log.LogDebug("  {File}: cache hit from blob ({Chars} chars)", file.FileName, cachedText.Length);
                    continue;
                }

                // ── CACHE MISS: download PDF → extract → save to blob ──
                if (string.IsNullOrEmpty(file.FilePath)) continue;
                try
                {
                    var blobClient = container.GetBlobClient(file.FilePath);
                    if (!await blobClient.ExistsAsync(ct)) continue;

                    var download = await blobClient.DownloadContentAsync(ct);
                    var pdfBytes = download.Value.Content.ToArray();

                    var layoutResult = await _docInt.AnalyzeDocumentAsync(
                        WaitUntil.Completed, "prebuilt-layout",
                        new MemoryStream(pdfBytes), cancellationToken: ct);

                    var richDoc = await _cleaner.CleanToRichDocumentAsync(
                        JsonSerializer.Serialize(layoutResult.Value), 
                        file.FileName ?? "unknown", 
                        file.DocumentType ?? "Supplementary", 
                        ct);

                    var text = JsonSerializer.Serialize(richDoc);
                    var pageCount = layoutResult.Value.Pages.Count;

                    // Save extracted text to BLOB (not SQL)
                    await extractedBlobClient.UploadAsync(new BinaryData(text), overwrite: true, ct);

                    // Only store lightweight metadata in SQL
                    file.ExtractedPageCount = pageCount;

                    extractedDocs.Add(new ExtractedDocument
                    {
                        FileUID = file.FileUID,
                        FileName = file.FileName ?? "unknown",
                        DocType = file.DocumentType ?? "Supplementary",
                        Text = text ?? string.Empty,
                        PageCount = pageCount
                    });

                    _log.LogInformation("  {File}: extracted {Pages} pages, {Chars} chars → saved to blob",
                        file.FileName ?? "unknown", pageCount, text?.Length ?? 0);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "  {File}: extraction failed, skipping", file.FileName);
                    await LogStep(jobId, $"Warning: skipping {file.FileName}", ct);
                }
            }

            // Save page counts to SQL (no large text — just int columns)
            await _db.SaveChangesAsync(ct);

            // Index new extracts for AISearch RAG
            foreach (var doc in extractedDocs)
            {
                var structuredTables = await _cleaner.ExtractTablesAsync(doc.Text, ct);
                if (structuredTables != null)
                {
                    _tableStore.SetTables(doc.FileUID, structuredTables);
                }

                var richDoc = await _cleaner.CleanToRichDocumentAsync(doc.Text, doc.FileName, doc.DocType, ct);
                doc.Text = richDoc.CleanText;

                var fileEntity = files.First(f => f.FileUID == doc.FileUID);
                await IndexDocumentChunksAsync(fileEntity, richDoc, ct);
            }

            var orchestratorLogger = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ILogger<SpotReportOrchestrator>>();
            var orchestrator = new SpotReportOrchestrator(_scopeFactory, _db, orchestratorLogger, _cleaner, _search, _calculator, _embeddings);
            
            var spotResult = await orchestrator.GenerateAsync(job.CompanyId, extractedDocs, ProjectName.AGONESPot.ToString(), ct);

            if (!spotResult.Success)
            {
                await FailReportAsync(jobId, job.CompanyId,
                    spotResult.ErrorMessage ?? "AI generation failed.", ct);
                return Fail(500, spotResult.ErrorMessage ?? "AI generation failed.");
            }

            _log.LogInformation("[REPORT] Step 4: Generated report in {Ms}ms", spotResult.DurationMs);

            // ────────────────────────────────────────────────────────
            // STEP 5: Save final report to SQL (not blob)
            // ────────────────────────────────────────────────────────
            await LogStep(jobId, "Step 5/6: Saving report to database...", ct);

            var report = await _db.SpotReports.FirstOrDefaultAsync(r => r.JobId == jobId, ct);
            if (report != null)
            {
                report.ReportMarkdown = spotResult.ReportMarkdown;
                report.ReportJsonContent = spotResult.ReportJson;
            }

            // ────────────────────────────────────────────────────────
            // STEP 5.5: Generate and Upload PDF to Blob Storage
            // ────────────────────────────────────────────────────────
            if (report != null && !string.IsNullOrEmpty(report.ReportJsonContent))
            {
                try
                {
                    await LogStep(jobId, "Step 5.5/6: Generating and uploading PDF report...", ct);
                    var pdfBytes = await _pdf.ConvertFromJsonAsync(report.ReportJsonContent);
                    
                    var blobName = $"reports/{job.CompanyId}/{report.ReportId}.pdf";
                    var bc = container.GetBlobClient(blobName);
                    await bc.UploadAsync(new MemoryStream(pdfBytes), true, ct);
                    
                    report.FileURL = bc.Uri.ToString();
                    _log.LogInformation("[REPORT] PDF generated and uploaded to {Url}", report.FileURL);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "[REPORT] PDF generation/upload failed, skipping optional file step.");
                    await LogStep(jobId, "Warning: PDF generation failed, skipping.", ct);
                }
            }

            // ────────────────────────────────────────────────────────
            // STEP 6: Mark job as done
            // ────────────────────────────────────────────────────────
            job.Status = nameof(JobState.Success);
            job.FinishedAt = DateTime.UtcNow;
            await LogStep(jobId, "Step 6/6: Complete!", ct);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("[REPORT] Done! Company={Company}, JobId={JobId}, Duration={Ms}ms",
                job.CompanyId, jobId, spotResult.DurationMs);

            return Ok("Report generated.", new
            {
                reportId = report?.ReportId,
                companyId = job.CompanyId,
                durationMs = spotResult.DurationMs
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[REPORT] Failed for job {JobId}", jobId);
            await FailReportAsync(jobId, job.CompanyId, ex.Message, ct);
            return Fail(500, $"Report generation failed: {ex.Message}");
        }
    }
