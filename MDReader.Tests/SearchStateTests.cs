using System.Collections.Generic;
using System.Threading.Tasks;
using MDReader.App;
using Xunit;

namespace MDReader.Tests;

public class SearchStateTests
{
    [Fact]
    public async Task Wraps_When_Reaching_End()
    {
        var state = new SearchState();
        var calls = new List<bool>();

        Task<bool> Finder(bool wrap)
        {
            calls.Add(wrap);
            return Task.FromResult(wrap);
        }

        var outcome = await state.FindNextAsync("test", Finder);

        Assert.Equal(SearchOutcome.WrappedFound, outcome);
        Assert.Equal(new[] { false, true }, calls);
    }

    [Fact]
    public async Task NoMatch_DoesNotWrapAgain()
    {
        var state = new SearchState();
        var callCount = 0;

        Task<bool> Finder(bool wrap)
        {
            callCount++;
            return Task.FromResult(false);
        }

        var outcome1 = await state.FindNextAsync("nomatch", Finder);
        var callsAfterFirst = callCount;
        var outcome2 = await state.FindNextAsync("nomatch", Finder);

        Assert.Equal(SearchOutcome.NoMatch, outcome1);
        Assert.Equal(SearchOutcome.NoMatch, outcome2);
        Assert.Equal(callsAfterFirst, callCount);
    }
}
