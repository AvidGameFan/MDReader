using System;
using Microsoft.Web.WebView2.Core;

namespace MDReader.App;

public static class PrintHelper
{
    public static async Task<(bool Success, string? Error)> TryPrintAsync(CoreWebView2? coreWebView2)
    {
        if (coreWebView2 == null)
        {
            return (false, "Print is not ready yet.");
        }

        try
        {
            var settings = coreWebView2.Environment?.CreatePrintSettings();
            if (settings == null)
            {
                return (false, "Print is not ready yet.");
            }

            settings.ShouldPrintHeaderAndFooter = false;
            var status = await coreWebView2.PrintAsync(settings);
            return ToResult(status);
        }
        catch (Exception ex)
        {
            return (false, $"Print failed: {ex.Message}");
        }
    }

    private static (bool Success, string? Error) ToResult(CoreWebView2PrintStatus status)
    {
        return status switch
        {
            CoreWebView2PrintStatus.Succeeded => (true, null),
            _ => (false, $"Print failed: {status}")
        };
    }
}
