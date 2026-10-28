using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using MagicStorage;

internal static class FeedbackReaderTests
{
    private static void Check(bool condition) { if (!condition) throw new Exception("Feedback reader assertion failed"); }
    public static void Main(string[] args)
    {
        // Parse responses produced by the server's real-SQL contract test.
        var list = FeedbackReader.Parse(File.ReadAllBytes(args[0]), false);
        Check(list.Items.Length == 30 && list.Items[0].Id == "32" && list.NextFeedbackId == "3");
        Check(list.Items[0].Title == "中文 \"标题\" 🐱 32");
        Check(list.Items[0].Description == "描述\n反斜线\\与<b>文字</b>");
        var detail = FeedbackReader.Parse(File.ReadAllBytes(args[1]), true);
        Check(detail.Item.Id == "32" && detail.Replies.Length == 100 && detail.NextReplyId == "100");
        Check(detail.Replies[0].IsAdmin && detail.Replies[0].Kind == "system" && detail.Replies[0].AuthorName == "作者");
        Check(detail.Replies[0].Body == "讨论 \"引号\"\n第 1 条 😀");
        foreach (string value in new[] { "{}", "{\"items\":[null]}", "{\"items\":[{\"id\":\"1\"}]}" })
        {
            bool rejected = false;
            try { FeedbackReader.Parse(Encoding.UTF8.GetBytes(value), false); }
            catch (SerializationException) { rejected = true; }
            Check(rejected);
        }
        Check(FeedbackReader.ReadAsync("").GetAwaiter().GetResult().ErrorKey == "MS_FeedbackNeedSteam");
        Console.WriteLine("PASS: server/client JSON contract, Chinese, emoji, escaping, pagination, invalid response handling and unavailable Steam ID.");
    }
}
