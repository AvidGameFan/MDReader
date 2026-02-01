using System;
using System.Threading.Tasks;

namespace MDReader.App;

public enum SearchOutcome
{
    Found,
    WrappedFound,
    NoMatch
}

public sealed class SearchState
{
    private string? _lastQuery;
    private bool _noMatch;

    public async Task<SearchOutcome> FindNextAsync(string query, Func<bool, Task<bool>> findAsync)
    {
        if (!string.Equals(query, _lastQuery, StringComparison.Ordinal))
        {
            _lastQuery = query;
            _noMatch = false;
        }

        if (_noMatch)
        {
            return SearchOutcome.NoMatch;
        }

        var found = await findAsync(false);
        if (found)
        {
            return SearchOutcome.Found;
        }

        var wrappedFound = await findAsync(true);
        if (wrappedFound)
        {
            return SearchOutcome.WrappedFound;
        }

        _noMatch = true;
        return SearchOutcome.NoMatch;
    }
}
