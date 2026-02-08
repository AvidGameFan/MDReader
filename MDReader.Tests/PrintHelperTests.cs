using MDReader.App;
using Xunit;

namespace MDReader.Tests;

public class PrintHelperTests
{
    [Fact]
    public async Task TryPrintAsync_WhenCoreMissing_ReturnsFalse()
    {
        var (success, error) = await PrintHelper.TryPrintAsync(null);

        Assert.False(success);
        Assert.Equal("Print is not ready yet.", error);
    }
}
