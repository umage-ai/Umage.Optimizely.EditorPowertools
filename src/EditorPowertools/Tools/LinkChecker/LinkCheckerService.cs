using UmageAI.Optimizely.EditorPowerTools.Tools.LinkChecker.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.LinkChecker;

public class LinkCheckerService
{
    private readonly LinkCheckerRepository _repository;

    public LinkCheckerService(LinkCheckerRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets all link check records from DDS.
    /// </summary>
    public IEnumerable<LinkCheckDto> GetAllLinks()
    {
        return _repository.GetAll().Select(MapToDto);
    }

    /// <summary>
    /// Gets only broken (invalid) link records.
    /// </summary>
    public IEnumerable<LinkCheckDto> GetBrokenLinks()
    {
        return _repository.GetByStatus(false).Select(MapToDto);
    }

    /// <summary>
    /// Gets link statistics.
    /// </summary>
    public LinkCheckerStatsDto GetStats()
    {
        var all = _repository.GetAll().ToList();
        return new LinkCheckerStatsDto
        {
            TotalLinks = all.Count,
            BrokenLinks = all.Count(r => !r.IsValid),
            ValidLinks = all.Count(r => r.IsValid),
            InternalLinks = all.Count(r => r.LinkType == "Internal"),
            ExternalLinks = all.Count(r => r.LinkType == "External")
        };
    }

    private static LinkCheckDto MapToDto(LinkCheckRecord record)
    {
        return new LinkCheckDto
        {
            ContentId = record.ContentId,
            ContentName = record.ContentName,
            ContentTypeName = record.ContentTypeName,
            PropertyName = record.PropertyName,
            Url = record.Url,
            FriendlyUrl = record.FriendlyUrl,
            TargetContentId = record.TargetContentId,
            LinkType = record.LinkType,
            StatusCode = record.StatusCode,
            StatusText = record.StatusText,
            IsValid = record.IsValid,
            Breadcrumb = record.Breadcrumb,
            EditUrl = record.EditUrl,
            UsedOn = record.UsedOn,
            UsedOnEditUrls = record.UsedOnEditUrls,
            LastChecked = record.LastChecked
        };
    }
}
