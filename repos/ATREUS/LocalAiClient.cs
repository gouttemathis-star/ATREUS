using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ATREUS;

public sealed class LocalAiClient
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task<LocalAiResult> AskAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<LocalAiMessage> conversation,
        CancellationToken cancellationToken = default)
    {
        await EnsureServerAvailableAsync(cancellationToken);
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        messages.AddRange(conversation.Select(message => new
        {
            role = message.IsUser ? "user" : "assistant",
            content = message.Text
        }));
        var requestJson = JsonSerializer.Serialize(new
        {
            model,
            stream = false,
            messages,
            options = new { temperature = 0.2 }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/chat")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new LocalAiResult(false, null,
                $"Le moteur IA local a répondu avec le code HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var content) ||
                string.IsNullOrWhiteSpace(content.GetString()))
            {
                return new LocalAiResult(false, null, "Le moteur IA local a renvoyé une réponse vide ou illisible.");
            }

            return new LocalAiResult(true, content.GetString()!.Trim(), null);
        }
        catch (JsonException)
        {
            return new LocalAiResult(false, null, "La réponse du moteur IA local n’est pas au format attendu.");
        }
    }

    private static async Task EnsureServerAvailableAsync(CancellationToken cancellationToken)
    {
        if (await IsServerAvailableAsync(cancellationToken))
        {
            return;
        }

        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Ollama", "ollama.exe");
        if (!File.Exists(executable))
        {
            throw new HttpRequestException("Ollama n’est pas installé dans son emplacement local standard.");
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["OLLAMA_NO_CLOUD"] = "1" }
            });
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new HttpRequestException("ATREUS n’a pas pu démarrer Ollama localement.", exception);
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsServerAvailableAsync(cancellationToken))
            {
                return;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new HttpRequestException("Ollama a été démarré, mais son API locale reste indisponible sur 127.0.0.1:11434.");
    }

    private static async Task<bool> IsServerAvailableAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var response = await HttpClient.GetAsync("http://127.0.0.1:11434/api/tags", timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"L’API Ollama locale a répondu avec le code HTTP {(int)response.StatusCode}.",
                    null,
                    response.StatusCode);
            }
            return true;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is null)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}

public sealed record LocalAiResult(bool Success, string? Answer, string? Error);
public sealed record LocalAiMessage(string Text, bool IsUser);
