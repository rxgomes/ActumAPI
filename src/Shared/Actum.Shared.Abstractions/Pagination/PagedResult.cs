namespace Actum.Shared.Abstractions.Pagination;

/// <summary>
/// One page of a search result, with the total number of records that match the search
/// </summary>
/// <typeparam name="T">Item type</typeparam>
/// <param name="Items">Items of the requested page, in the search order</param>
/// <param name="Page">Page number, starting at 1</param>
/// <param name="PageSize">Maximum number of items per page</param>
/// <param name="TotalCount">Total number of records that match the search, across all pages</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
