namespace LivingWorld.AI;

/// <summary>Guarda de egress de rede do provider de LLM. Com <see cref="EnvVar"/>=1 (o gate liga em <c>scripts/test.sh</c>) todo
/// <see cref="HttpClient"/> criado por <see cref="Handler"/> lança <see cref="EgressBlockedException"/> em vez de sair para a rede,
/// então nenhum teste fala com um LLM real nem por engano de configuração.</summary>
public static class EgressGuard
{
    public const string EnvVar = "LW_BLOCK_EGRESS";

    public static bool BlockedByEnvironment => Environment.GetEnvironmentVariable(EnvVar) == "1";

    /// <summary>Handler que recusa qualquer requisição; <paramref name="blocked"/> padrão vem do ambiente.</summary>
    public static HttpMessageHandler Handler(bool? blocked = null) =>
        blocked ?? BlockedByEnvironment ? new BlockingHandler() : new SocketsHttpHandler();

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new EgressBlockedException(request.RequestUri?.ToString() ?? "?");
    }
}

/// <summary>Lançada pelo guarda: distingue "bloqueado de propósito" de "sem rede no ambiente".</summary>
public sealed class EgressBlockedException(string target) : Exception($"egress de rede bloqueado pelo guard: {target}");
