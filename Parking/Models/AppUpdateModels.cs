using System;

namespace Parking.Models;

public class AppReleaseInfoDto
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public DateTime? ReleaseDateUtc { get; set; }
    public string? ReleaseNotes { get; set; }
    public string PackageSha256 { get; set; } = string.Empty;
    public long PackageSizeBytes { get; set; }
    public string DownloadEndpoint { get; set; } = string.Empty;
}

public class UpdateProgressReport
{
    public string StepDescription { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public bool IsIndeterminate { get; set; }
    public bool IsError { get; set; }
    public string? ErrorMessage { get; set; }
}
