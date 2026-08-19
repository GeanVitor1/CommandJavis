using Vox;

namespace Vox.Tests;

public class CalculatorTests
{
    [Theory]
    [InlineData("15 vezes 3", 45)]
    [InlineData("2 mais 2", 4)]
    [InlineData("10 dividido por 4", 2.5)]
    [InlineData("5 menos 2", 3)]
    [InlineData("2 elevado a 3", 8)]
    [InlineData("dois mais dois", 4)]
    [InlineData("vinte e cinco", 25)]
    [InlineData("dois mil e vinte", 2020)]
    [InlineData("cento e vinte cinco", 125)]
    [InlineData("100 menos 25", 75)]
    [InlineData("7 vezes 8", 56)]
    [InlineData("10 mais 5 vezes 2", 20)]
    [InlineData("20 porcento de 50", 10)]
    [InlineData("2,5 mais 2,5", 5)]
    public void Evaluate_computes_expressions(string expr, double expected)
    {
        var result = Calculator.Evaluate(expr);
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value, 3);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("15 vezes")]
    public void Evaluate_returns_null_for_invalid(string expr)
    {
        Assert.Null(Calculator.Evaluate(expr));
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("cinco", 5)]
    [InlineData("quinze", 15)]
    [InlineData("cem", 100)]
    [InlineData("trinta e dois", 32)]
    public void TryParseNumber_reads_words(string input, double expected)
    {
        Assert.True(Calculator.TryParseNumber(input, out var value));
        Assert.Equal(expected, value, 3);
    }
}

public class SearchEnginesTests
{
    [Theory]
    [InlineData("https://www.youtube.com", "coldplay paradise", "https://www.youtube.com/results?search_query=coldplay%20paradise")]
    [InlineData("https://www.google.com.br", "gato fofo", "https://www.google.com/search?q=gato%20fofo")]
    [InlineData("https://pt.wikipedia.org", "brasil", "https://pt.wikipedia.org/w/index.php?search=brasil")]
    [InlineData("https://www.netflix.com", "stranger things", "https://www.netflix.com/search?q=stranger%20things")]
    [InlineData("https://x.com", "openai", "https://twitter.com/search?q=openai")]
    [InlineData("https://lista.mercadolivre.com.br", "tv 4k", "https://lista.mercadolivre.com.br/tv%204k")]
    public void Build_uses_known_template(string target, string query, string expected)
    {
        var url = SearchEngines.Build(null, target, query);
        Assert.Equal(expected, url);
    }

    [Fact]
    public void Build_prefers_custom_template()
    {
        var template = "https://exemplo.com/busca?q={q}&x=1";
        var url = SearchEngines.Build(template, "https://www.youtube.com", "teste");
        Assert.Equal("https://exemplo.com/busca?q=teste&x=1", url);
    }

    [Fact]
    public void Build_returns_null_for_unknown_host()
    {
        Assert.Null(SearchEngines.Build(null, "https://www.meusite-inexistente-xyz.com", "algo"));
    }

    [Fact]
    public void SupportsSearch_recognizes_youtube()
    {
        Assert.True(SearchEngines.SupportsSearch("https://www.youtube.com"));
        Assert.False(SearchEngines.SupportsSearch("https://meusite-inexistente-xyz.com"));
    }
}