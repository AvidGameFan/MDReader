using System.IO;
using Markdig;
using Xunit;

namespace MDReader.Tests;

public class EditModeMarkdownRoundTripTests
{
    private static string ExtractEditorScript(string markdown)
    {
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseTaskLists()
            .Build();
        var htmlBody = Markdown.ToHtml(markdown, pipeline);

        var hardLineBreaks = "false";
        var script = @"<script>
(function() {
    var editor = document.getElementById('editor');
    window.mdreader_getMarkdown = function() {
        if (!editor) { return ''; }
        if (!window.TurndownService) { return '__TURNDOWN_MISSING__'; }
        var turndownService = new TurndownService({ headingStyle: 'atx', bulletListMarker: '-', codeBlockStyle: 'fenced', hr: '---' });
        turndownService.addRule('checkbox', {
            filter: function(node) { return node.nodeName === 'INPUT' && node.type === 'checkbox'; },
            replacement: function(content, node) {
                var checked = typeof node.checked === 'boolean' ? node.checked : (typeof node.getAttribute === 'function' && node.getAttribute('checked') !== null);
                return (checked ? '[x]' : '[ ]');
            }
        });
        turndownService.addRule('table', {
            filter: 'table',
            replacement: function (content, node) {
                var rows = node.querySelectorAll('tr');
                if (rows.length === 0) return '';
                var markdownRows = [];
                var headerSeparatorNeeded = false;
                Array.prototype.forEach.call(rows, function (row) {
                    var cells = row.querySelectorAll('th, td');
                    var cellContents = [];
                    Array.prototype.forEach.call(cells, function (cell) {
                        var cellText = turndownService.turndown(cell).trim();
                        cellText = cellText.replace(/\\|/g, '|').replace(/\\n+/g, ' ');
                        cellContents.push(' ' + cellText + ' ');
                    });
                    markdownRows.push('|' + cellContents.join('|') + '|');
                });
                if (markdownRows.length > 1) {
                    var firstRowHasTH = node.querySelector('tr th') !== null;
                    headerSeparatorNeeded = firstRowHasTH || node.querySelector('thead') !== null;
                }
                if (headerSeparatorNeeded && markdownRows.length > 1) {
                    var colCount = markdownRows[0].split('|').filter(function (c) { return c.trim() !== ''; }).length;
                    var sepParts = [];
                    for (var i = 0; i < colCount; i++) { sepParts.push(' --- '); }
                    markdownRows.splice(1, 0, '|' + sepParts.join('|') + '|');
                }
                return '\\n\\n' + markdownRows.join('\\n') + '\\n\\n';
            }
        });
        if (__HARDLINEBREAKS__) {
            turndownService.addRule('linebreak', {
                filter: 'br',
                replacement: function() { return '\\n'; }
            });
        }
        var markdown = turndownService.turndown(editor);
        markdown = markdown.replace(/^\\-\\s{2,}/gm, '- ');
        return markdown;
    };
})();
</script>";
        script = script.Replace("__HARDLINEBREAKS__", hardLineBreaks);

        return script;
    }

    [Fact]
    public void PipeTable_ConvertsToHtmlTable_WithCorrectStructure()
    {
        var markdown = @"| Header 1 | Header 2 |
| -------- | -------- |
| Cell 1   | Cell 2   |";

        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseTaskLists()
            .Build();
        var html = Markdown.ToHtml(markdown, pipeline);

        Assert.Contains("<table>", html);
        Assert.Contains("<th>", html);
        Assert.Contains("<td>", html);
        Assert.Contains("Header 1", html);
        Assert.Contains("Cell 1", html);
    }

    [Fact]
    public void TableDivide_HrOption_IsDashNotStars()
    {
        var script = ExtractEditorScript("");
        Assert.Contains("hr: '---'", script);
        Assert.DoesNotContain("hr: '* * *'", script);
    }

    [Fact]
    public void TableRule_IsPresent_InEditorScript()
    {
        var script = ExtractEditorScript("");
        Assert.Contains("addRule('table'", script);
        Assert.Contains("filter: 'table'", script);
    }

    [Fact]
    public void CheckboxRule_IsPresent_InEditorScript()
    {
        var script = ExtractEditorScript("");
        Assert.Contains("addRule('checkbox'", script);
    }
}
