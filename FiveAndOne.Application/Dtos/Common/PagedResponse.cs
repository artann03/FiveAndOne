namespace FiveAndOne.Application.Dtos.Common;

public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResponse<T> Create(List<T> items, int page, int pageSize, int totalCount) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalCount = totalCount
    };
}

/// <summary>Body for actions that need a reason (cancel, suspend, ...).</summary>
public class ReasonRequest
{
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Reason can be at most 500 characters.")]
    public string? Reason { get; set; }
}

/// <summary>Body for actions where a reason is mandatory.</summary>
public class RequiredReasonRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "A reason is required.")]
    [System.ComponentModel.DataAnnotations.StringLength(500, ErrorMessage = "Reason can be at most 500 characters.")]
    public string? Reason { get; set; }
}
