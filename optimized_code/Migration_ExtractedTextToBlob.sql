-- ═══════════════════════════════════════════════════════════════════
-- MIGRATION: Move ExtractedText from SQL to Blob Storage
-- Run this AFTER deploying the new code.
-- ═══════════════════════════════════════════════════════════════════

-- Step 1: Add new columns
ALTER TABLE SpotDocuments ADD ExtractedBlobPath NVARCHAR(500) NULL;
ALTER TABLE SpotDocuments ADD ExtractedAt       DATETIME2     NULL;
GO

-- Step 2: After code is deployed and all files have been re-extracted
--         to blob (happens automatically on next report run), drop the
--         old column to reclaim space.
--
-- ⚠️  DO NOT RUN THIS until you've confirmed blob extraction is working!
--
-- ALTER TABLE SpotDocuments DROP COLUMN ExtractedText;
-- GO
