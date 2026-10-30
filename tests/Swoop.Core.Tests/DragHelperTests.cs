using Swoop.Core;
using Xunit;

namespace Swoop.Core.Tests;

public class DragHelperTests
{
    [Fact]
    public void Extract_several_links_dedupe_trim()
        => Assert.Equal(
            new[] { "https://youtu.be/a", "https://youtu.be/b" },
            DragHelper.ExtractLinks([" https://youtu.be/a ", "https://youtu.be/b", "https://youtu.be/a"]));

    [Fact]
    public void Extract_garbage_yields_empty()
        => Assert.Empty(DragHelper.ExtractLinks(["hello world", "nope"]));

    [Fact]
    public void Extract_youtube_variants_multi_line()
        => Assert.Equal(
            new[] { "https://www.youtube.com/watch?v=x&feature=share", "music.youtube.com/watch?v=w", "https://www.youtube.com/playlist?list=xyz" },
            DragHelper.ExtractLinks([
                "https://www.youtube.com/watch?v=x&feature=share",
                "music.youtube.com/watch?v=w",
                "",
                "https://www.youtube.com/playlist?list=xyz"]));

    [Theory]
    [InlineData("Check out soundcloud.com/artist/track !")]
    [InlineData("soundcloud.com/artist/track")]
    public void Extract_treats_urlish_text_as_link(string line)
        => Assert.Contains("soundcloud.com/artist/track", DragHelper.ExtractLinks([line]));
}

