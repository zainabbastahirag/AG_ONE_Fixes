using AgoneSentimentSales.Domain.Entities;
using AgoneSentimentSales.Domain.Enums;
using AgoneSentimentSales.Domain.Interfaces;

namespace AgoneSentimentSales.Infrastructure.Services;

/// <summary>
/// Seeds top LSE-listed companies (FTSE 100 representative set) for MVP.
/// Production replaces this with LSE/market data API ingestion.
/// </summary>
public class LseSampleDataProvider : ICompanyDataProvider
{
    private static readonly (string Name, string Ticker, string Sector, decimal MktCapB)[] Seed =
    [
        ("Shell plc", "SHEL", "Energy", 168m), ("AstraZeneca PLC", "AZN", "Healthcare", 210m),
        ("HSBC Holdings plc", "HSBA", "Banking & Financial Services", 148m),
        ("Unilever PLC", "ULVR", "Consumer & Retail", 112m), ("BP p.l.c.", "BP", "Energy", 95m),
        ("GSK plc", "GSK", "Healthcare", 78m), ("Rio Tinto plc", "RIO", "Materials & Mining", 88m),
        ("Barclays PLC", "BARC", "Banking & Financial Services", 32m),
        ("Lloyds Banking Group plc", "LLOY", "Banking & Financial Services", 38m),
        ("Diageo plc", "DGE", "Consumer & Retail", 58m), ("British American Tobacco", "BATS", "Consumer & Retail", 72m),
        ("BT Group plc", "BT.A", "Telecommunications", 18m), ("Vodafone Group Plc", "VOD", "Telecommunications", 22m),
        ("National Grid plc", "NG.", "Energy & Utilities", 42m), ("Rolls-Royce Holdings", "RR.", "Industrials", 48m),
        ("BAE Systems plc", "BA.", "Industrials", 52m), ("RELX PLC", "REL", "Technology & Services", 62m),
        ("London Stock Exchange Group", "LSEG", "Banking & Financial Services", 58m),
        ("Prudential plc", "PRU", "Insurance", 28m), ("Aviva plc", "AV.", "Insurance", 14m),
        ("Standard Chartered", "STAN", "Banking & Financial Services", 24m),
        ("Anglo American plc", "AAL", "Materials & Mining", 26m), ("Glencore plc", "GLEN", "Materials & Mining", 55m),
        ("Compass Group PLC", "CPG", "Consumer & Retail", 48m), ("Reckitt Benckiser", "RKT", "Consumer & Retail", 44m),
        ("Experian plc", "EXPN", "Technology & Services", 38m), ("Sage Group plc", "SGE", "Technology & Services", 12m),
        ("Ashtead Group plc", "AHT", "Construction & Infrastructure", 28m),
        ("Persimmon plc", "PSN", "Property & REITs", 6m), ("Segro plc", "SGRO", "Property & REITs", 12m),
        ("Taylor Wimpey plc", "TW.", "Property & REITs", 5m), ("Whitbread PLC", "WTB", "Consumer & Retail", 8m),
        ("InterContinental Hotels", "IHG", "Consumer & Retail", 14m), ("Next plc", "NXT", "Consumer & Retail", 16m),
        ("Tesco PLC", "TSCO", "Consumer & Retail", 24m), ("Sainsbury (J) plc", "SBRY", "Consumer & Retail", 7m),
        ("Marks and Spencer", "MKS", "Consumer & Retail", 5m), ("Ocado Group plc", "OCDO", "Consumer & Retail", 4m),
        ("SSE plc", "SSE", "Energy & Utilities", 18m), ("Centrica plc", "CNA", "Energy & Utilities", 8m),
        ("United Utilities", "UU.", "Energy & Utilities", 7m), ("Severn Trent plc", "SVT", "Energy & Utilities", 7m),
        ("WPP plc", "WPP", "Media", 6m), ("Informa plc", "INF", "Media", 12m),
        ("Smith & Nephew plc", "SN.", "Healthcare", 14m), ("Haleon plc", "HLN", "Healthcare", 32m),
        ("Hikma Pharmaceuticals", "HIK", "Healthcare", 5m), ("Convatec Group PLC", "CTEC", "Healthcare", 4m),
        ("Fresnillo plc", "FRES", "Materials & Mining", 6m), ("Antofagasta plc", "ANTO", "Materials & Mining", 22m),
        ("Melrose Industries", "MRO", "Industrials", 6m), ("Weir Group PLC", "WEIR", "Industrials", 6m),
        ("IMI plc", "IMI", "Industrials", 5m), ("Halma plc", "HLMA", "Industrials", 12m),
        ("Spirax-Sarco", "SPX", "Industrials", 10m), ("Smiths Group plc", "SMIN", "Industrials", 8m),
        ("3i Group plc", "III", "Banking & Financial Services", 22m), ("M&G plc", "MNG", "Insurance", 5m),
        ("Legal & General", "LGEN", "Insurance", 14m), ("Phoenix Group", "PHNX", "Insurance", 6m),
        ("Admiral Group plc", "ADM", "Insurance", 8m), ("Beazley plc", "BEZ", "Insurance", 6m),
        ("Rightmove plc", "RMV", "Technology & Services", 6m), ("Auto Trader Group", "AUTO", "Technology & Services", 7m),
        ("Softcat plc", "SCT", "Technology & Services", 4m), ("Computacenter plc", "CCC", "Technology & Services", 5m),
        ("Diploma PLC", "DPLM", "Industrials", 6m), ("Rentokil Initial", "RTO", "Industrials", 12m),
        ("DCC plc", "DCC", "Industrials", 6m), ("Entain plc", "ENT", "Consumer & Retail", 6m),
        ("Flutter Entertainment", "FLTR", "Consumer & Retail", 28m), ("JD Sports Fashion", "JD.", "Consumer & Retail", 8m),
        ("Burberry Group plc", "BRBY", "Consumer & Retail", 4m), ("Associated British Foods", "ABF", "Consumer & Retail", 16m),
        ("Bunzl plc", "BNZL", "Industrials", 10m), ("Croda International", "CRDA", "Materials & Mining", 6m),
        ("Johnson Matthey", "JMAT", "Materials & Mining", 4m), ("Smurfit Westrock", "SW", "Materials & Mining", 10m),
        ("International Distributions", "IDS", "Industrials", 4m), ("Pearson plc", "PSON", "Media", 7m),
        ("Hargreaves Lansdown", "HL.", "Banking & Financial Services", 5m), ("St. James's Place", "SJP", "Banking & Financial Services", 4m),
        ("Man Group plc", "EMG", "Banking & Financial Services", 4m), ("Schroders plc", "SDR", "Banking & Financial Services", 7m),
        ("Investec plc", "INVP", "Banking & Financial Services", 4m), ("Close Brothers", "CBG", "Banking & Financial Services", 1.5m),
        ("Direct Line Insurance", "DLG", "Insurance", 2.5m), ("Hiscox Ltd", "HSX", "Insurance", 4m),
        ("S4 Capital plc", "SFOR", "Media", 0.5m), ("Wisesoft plc placeholder", "WISE", "Technology & Services", 8m),
        ("Darktrace plc", "DARK", "Technology & Services", 3m), ("Alphawave IP Group", "AWE", "Technology & Semiconductors", 2m),
        ("Arm Holdings plc", "ARM", "Technology & Semiconductors", 120m), ("Micro Focus legacy slot", "MCRO", "Technology & Services", 2m),
        ("BT Openreach slot", "BTOR", "Telecommunications", 18m), ("Capita plc", "CPI", "Technology & Services", 0.4m),
        ("Serco Group plc", "SRP", "Technology & Services", 2m), ("Kainos Group plc", "KNOS", "Technology & Services", 1.5m),
        ("FDM Group Holdings", "FDM", "Technology & Services", 1.2m), ("Bytes Technology", "BYIT", "Technology & Services", 1m),
        ("NCC Group plc", "NCC", "Technology & Services", 0.6m), ("Redrow plc", "RDW", "Property & REITs", 2m),
        ("Barratt Developments", "BDEV", "Property & REITs", 4m), ("Berkeley Group", "BKG", "Property & REITs", 5m),
        ("Land Securities", "LAND", "Property & REITs", 5m), ("British Land", "BLND", "Property & REITs", 4m),
        ("Hammerson plc", "HMSO", "Property & REITs", 1m), ("Big Yellow Group", "BYG", "Property & REITs", 2m)
    ];

    public Task<IReadOnlyList<LseCompany>> GetTopCompaniesByMarketCapAsync(int count, CancellationToken cancellationToken = default)
    {
        var list = Seed
            .OrderByDescending(x => x.MktCapB)
            .Take(count)
            .Select((x, i) => new LseCompany
            {
                Rank = i + 1,
                CompanyName = x.Name,
                Ticker = x.Ticker,
                Sector = x.Sector,
                IndustryGroup = x.Sector,
                MarketCapGbpB = x.MktCapB,
                HqLocation = "London, UK",
                OffshoringStatus = OffshoringStatus.Unknown,
                DataSourceNotes = "MVP seed — replace with LSE/market data API"
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<LseCompany>>(list);
    }
}
