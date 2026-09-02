namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, T9 (LLM-14, story "Segurança de rede e injeção", AC1): guard de runtime que
/// bloqueia qualquer egress de rede por comportamento — nunca por lista de tipos banidos. Dois
/// pontos de bloqueio independentes cobrem transporte novo:
/// <see cref="Handler"/> (todo <c>HttpMessageHandler</c>/<c>HttpClient</c>, inclusive gRPC, que
/// hoje roda sobre <c>HttpMessageHandler</c>) e <see cref="ConnectCallback"/> (conexão de baixo
/// nível via <c>SocketsHttpHandler</c>, antes até da resolução de DNS — cobre socket/WebSocket
/// futuro que reuse o mesmo pipeline de conexão). Nenhum dos dois inspeciona o destino: qualquer
/// tentativa lança, de propósito — é o próprio critério de verificação do spec.md.</summary>
public class NetworkEgressGuardTests
{
    /// <summary>Lançada pelo guard — nunca uma exceção genérica de rede (timeout/DNS), para que
    /// o teste distinga "bloqueado de propósito" de "sem rede no ambiente".</summary>
    public sealed class NetworkEgressBlockedException(string context) : Exception($"egress de rede bloqueado pelo guard: {context}");

    /// <summary>Bloqueia no nível de <c>HttpMessageHandler</c> — cobre qualquer <c>HttpClient</c>
    /// construído sobre este handler, inclusive chamadas gRPC (que também usam <c>HttpMessageHandler</c>).</summary>
    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NetworkEgressBlockedException($"HttpClient.SendAsync -> {request.RequestUri}");
    }

    /// <summary>Bloqueia no nível de conexão de baixo nível de um <see cref="SocketsHttpHandler"/>
    /// — roda antes da resolução de DNS/abertura de socket, então cobre transporte que não passe
    /// por <see cref="Handler"/> mas ainda use o pipeline de conexão do <c>SocketsHttpHandler</c>.</summary>
    private static ValueTask<Stream> ConnectCallback(SocketsHttpConnectionContext context, CancellationToken cancellationToken) =>
        throw new NetworkEgressBlockedException($"SocketsHttpHandler.ConnectCallback -> {context.DnsEndPoint}");

    [Fact]
    public async Task Http_client_over_the_guarded_handler_throws_on_any_connection_attempt()
    {
        using var client = new HttpClient(new Handler());

        await Assert.ThrowsAsync<NetworkEgressBlockedException>(() => client.GetAsync("http://example.com"));
    }

    [Fact]
    public async Task Low_level_connect_callback_throws_before_dns_resolution_or_socket_open()
    {
        using var client = new HttpClient(new SocketsHttpHandler { ConnectCallback = ConnectCallback });

        // SocketsHttpHandler embrulha a exceção do ConnectCallback em HttpRequestException — o
        // guard em si (InnerException) é a prova de que o bloqueio disparou antes de qualquer
        // DNS/socket real, não um erro de rede incidental do ambiente.
        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://example.com"));
        Assert.IsType<NetworkEgressBlockedException>(thrown.InnerException);
    }
}
