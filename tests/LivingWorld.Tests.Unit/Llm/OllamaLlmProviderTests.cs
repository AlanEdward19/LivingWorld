using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LivingWorld.AI;
using LivingWorld.Domain.Llm;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11 (ADR-0016 follow-up): <see cref="OllamaLlmProvider"/> é só transporte —
/// monta a requisição HTTP certa e faz o parse certo. Nunca chama rede real (mesmo guard de T9,
/// <see cref="NetworkEgressGuardTests"/>): o <see cref="HttpMessageHandler"/> aqui é 100% fake,
/// nunca abre socket/DNS de verdade.</summary>
public class OllamaLlmProviderTests
{
    /// <summary>Handler fake que devolve uma resposta fabricada e captura a última requisição
    /// para o teste inspecionar o corpo montado — mesmo espírito do <c>Handler</c> de
    /// <see cref="NetworkEgressGuardTests"/>, mas devolvendo um corpo em vez de lançar.</summary>
    private sealed class FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public JsonElement LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var bodyText = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            LastBody = JsonSerializer.Deserialize<JsonElement>(bodyText);
            return respond(request, bodyText);
        }
    }

    private static LlmContext BaseContext() => new(
        NpcKnowledgeSummary: "Ana, ferreira, Vilarejo",
        PlayerUtterance: "oi, tudo bem?",
        AllowedIntents: ["greet", "trade"],
        AllowedActions: ["CHANGE_RELATION:1", "END_CONVERSATION:0"]);

    private static HttpResponseMessage OllamaChatResponse(string dialogue, string emotion, object[] proposedActions)
    {
        var structuredOutput = JsonSerializer.Serialize(new
        {
            dialogue,
            emotion,
            proposedActions,
        });

        var payload = new
        {
            model = "qwen3.5:4b",
            message = new { role = "assistant", content = structuredOutput },
            done = true,
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload),
        };
    }

    [Fact]
    public async Task Builds_the_request_with_model_options_and_schema_from_ADR_0016()
    {
        var handler = new FakeHandler((_, _) => OllamaChatResponse("oi!", "friendly", []));
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        await provider.GetResponseAsync(BaseContext());

        Assert.Equal("http://localhost:11434/api/chat", handler.LastRequest!.RequestUri!.ToString());

        var body = handler.LastBody;
        Assert.Equal("qwen3.5:4b", body.GetProperty("model").GetString());
        Assert.False(body.GetProperty("stream").GetBoolean());
        Assert.False(body.GetProperty("think").GetBoolean());

        var options = body.GetProperty("options");
        Assert.Equal(0, options.GetProperty("temperature").GetInt32());
        Assert.Equal(4096, options.GetProperty("num_ctx").GetInt32());
        Assert.Equal(256, options.GetProperty("num_predict").GetInt32());

        var format = body.GetProperty("format");
        Assert.Equal("neutral", format.GetProperty("properties").GetProperty("emotion").GetProperty("enum")[0].GetString());

        var messages = body.GetProperty("messages");
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("oi, tudo bem?", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Parses_a_valid_ollama_response_into_LlmResponse()
    {
        var handler = new FakeHandler((_, _) => OllamaChatResponse(
            "Bem-vindo à forja!", "friendly",
            [new { type = "CHANGE_RELATION", magnitude = 1 }, new { type = "NONE", magnitude = 0 }]));
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        var response = await provider.GetResponseAsync(BaseContext());

        Assert.Equal("Bem-vindo à forja!", response.Dialogue);
        Assert.Equal("friendly", response.Emotion);
        Assert.Equal("CHANGE_RELATION", response.Intent);
        Assert.Equal(["CHANGE_RELATION:1"], response.ProposedActions);
        Assert.Empty(response.MemoryCandidates);
    }

    [Fact]
    public async Task Empty_proposed_actions_yield_empty_intent_and_empty_actions()
    {
        var handler = new FakeHandler((_, _) => OllamaChatResponse("oi", "neutral", []));
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        var response = await provider.GetResponseAsync(BaseContext());

        Assert.Equal("", response.Intent);
        Assert.Empty(response.ProposedActions);
    }

    [Fact]
    public async Task Network_failure_propagates_instead_of_being_swallowed_or_fixed_up()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("simulated network failure"));
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetResponseAsync(BaseContext()));
    }

    [Fact]
    public async Task Non_success_status_code_propagates_as_an_exception()
    {
        var handler = new FakeHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetResponseAsync(BaseContext()));
    }

    [Fact]
    public async Task Invalid_structured_output_json_propagates_instead_of_being_normalized()
    {
        var handler = new FakeHandler((_, _) =>
        {
            var payload = new { model = "qwen3.5:4b", message = new { role = "assistant", content = "not-json-at-all" } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
        });
        using var client = new HttpClient(handler);
        var provider = new OllamaLlmProvider(client);

        await Assert.ThrowsAsync<JsonException>(() => provider.GetResponseAsync(BaseContext()));
    }
}
