
namespace AgoneSentimentSales.Shared.Constants;

public static class DataSourceTypes
{
    public const string AnnualReport = "AnnualReport";
    public const string LinkedIn = "LinkedIn";
    public const string JobBoard = "JobBoard";
    public const string PressRelease = "PressRelease";
    public const string CompanyWebsite = "CompanyWebsite";
    public const string InvestorRelations = "InvestorRelations";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All =
        [AnnualReport, LinkedIn, JobBoard, PressRelease, CompanyWebsite, InvestorRelations, Other];
}
