using System.ClientModel;
using System.Text.Json;
using BookAI.Services.Abstraction;
using BookAI.Services.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Polly;
using ChatMessage = OpenAI.Chat.ChatMessage;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace BookAI.Services;

public class AIService(ChatClient chatClient, ILogger<AIService> logger) : IAIService
{
    private readonly ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
        .AddRetry(new()
        {
            ShouldHandle = new PredicateBuilder().Handle<ClientResultException>(e => e.Message.Contains("rate_limit_exceeded")),
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(50),
            OnRetry = r =>
            {
                logger.LogWarning("Throttling, attempt {AttemptNumber}", r.AttemptNumber);
                return ValueTask.CompletedTask;
            },
        })
        .AddTimeout(TimeSpan.FromMinutes(1))
        .Build();

    public async Task<ExplanationResponse> ExplainAsync(string sentence, Chunk chunk, CancellationToken cancellationToken)
    {
        return await RetryOpenAIAsync(() => InternalExplainAsync(sentence, chunk, cancellationToken));
    }

    public async Task<ConfusionResponse> EvaluateConfusionAsync(Chunk chunk, CancellationToken cancellationToken)
    {
        return await RetryOpenAIAsync(() => InternalEvaluateConfusionAsync(chunk, cancellationToken));
    }

    public async Task<EndnotesFixupResponse> FixupEndnotesAsync(string html, CancellationToken cancellationToken)
    {
        return await RetryOpenAIAsync(() => InternalFixupEndnotesAsync(html, cancellationToken));
    }

    private async Task<T> RetryOpenAIAsync<T>(Func<Task<T>> func)
    {
        return await pipeline.ExecuteAsync(async _ => await func());
    }

    private async Task<ExplanationResponse> InternalExplainAsync(string sentence, Chunk chunk, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending explanation request");

        const string schemaJson = """
                                  {
                                      "required": ["contextExplanation", "sentenceExplanation"],
                                      "type": "object",
                                      "properties": {
                                          "contextExplanation": {
                                              "type": "string"
                                          },
                                          "sentenceExplanation": {
                                            "type": "string"
                                          }
                                      },
                                      "additionalProperties": false
                                  }
                                  """;


        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("json_schema", BinaryData.FromString(schemaJson), jsonSchemaIsStrict: true);

        var response = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                    I'm going to send you a piece of text. Please explain what it is about in clear, everyday language. Avoid using the same technical terms or phrases found in the original text. Rephrase ideas into simple, accessible words that reader can understand.
                                    Context:
                                    ```
                                    {chunk.Context}
                                 
                                    {chunk.Text}
                                    ```
                                 
                                    Also, please clarify the meaning of the following sentence, as it appears particularly confusing and provide a footnote that can be added to the end of the sentence as the footnote for that sentence:
                                    Sentence: '{sentence}'.
                                 
                                    1. Briefly explain the context
                                    2. Explain the sentence '{sentence}'
                                    3. Please avoid starting the explanation like 'This sentence means...' or 'That sentence is...' or something like that.
                                 
                                    Return a JSON with the followiong properties:
                                    "contextExplanation": a text explanation for the context
                                    "sentenceExplanation": the explanation for the sentence (i.e. the footnote)
                                 """)
        }, new ChatCompletionOptions
        {
            ResponseFormat = chatResponseFormat
        }, cancellationToken: cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ExplanationResponse>(response.Value.Content[0].Text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to deserialize response to confusion response. {@Response}", response);
            throw;
        }
    }

    private async Task<ConfusionResponse> InternalEvaluateConfusionAsync(Chunk chunk, CancellationToken cancellationToken)
    {
        const string schemaJson = """
                                  {
                                      "required": ["textConfusionScores"],
                                      "type": "object",
                                      "properties": {
                                          "textConfusionScores": {
                                              "type": "array",
                                              "items": {
                                                  "type": "object",
                                                  "properties": {
                                                      "text": { "type": "string" },
                                                      "confusionScore": { "type": "integer" }
                                                  },
                                                  "required": ["text", "confusionScore"],
                                                  "additionalProperties": false
                                              }
                                          }
                                      },
                                      "additionalProperties": false
                                  }
                                  """;


        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("json_schema", BinaryData.FromString(schemaJson), jsonSchemaIsStrict: true);

        logger.LogDebug("Sending confusion request");

        var response = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                 I'm going to send you a text chunk. Please perform the following steps:

                                 1. Summarize what the text is about in simple, clear language.
                                 2. Identify any sentences or segments that might be confusing or complex.
                                 3. For each confusing sentence or segment, assign a confusion score from 0 to 10 (0 means very clear; 10 means very confusing). When rating, focus on factors such as ambiguous phrasing, complex sentence structure, and difficult vocabulary. While sentence length may influence clarity, treat it only as a secondary factor in your scoring.

                                 For context, here is some previously provided text:```
                                 {chunk.Context}
                                 ```

                                 And here is the current text chunk:
                                 ```
                                 {chunk.Text}
                                 ```

                                 Please return your results as a JSON object with a "textConfusionScores" array. Each entry in the array should be an object with:
                                 - "text": (the exact sentence or segment identified as confusing)
                                 - "confusionScore": (an integer from 0 to 10)
                                 """)
        }, new ChatCompletionOptions
        {
            ResponseFormat = chatResponseFormat
        }, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<ConfusionResponse>(response.Value.Content[0].Text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to deserialize response to confusion response. Defaulting to empty response. {@Response}", response);
            return new ConfusionResponse() { TextConfusionScores = Array.Empty<TextConfusionScore>() };
        }
    }

    private async Task<EndnotesFixupResponse> InternalFixupEndnotesAsync(string html, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending endnotes fixup request");

        const string schemaJson = """
                                  {
                                      "required": ["fixedHtml"],
                                      "type": "object",
                                      "properties": {
                                          "fixedHtml": {
                                              "type": "string"
                                          }
                                      },
                                      "additionalProperties": false
                                  }
                                  """;


        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("json_schema", BinaryData.FromString(schemaJson), jsonSchemaIsStrict: true);

        var response = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                    I'm going to send you HTML code containing endnotes from the book. Please remove duplicate information.
                                    HTML:
                                    ```
                                    {html}
                                    ```
                                 """)
        }, new ChatCompletionOptions
        {
            ResponseFormat = chatResponseFormat
        }, cancellationToken: cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<EndnotesFixupResponse>(response.Value.Content[0].Text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to deserialize endnotes fixup response to confusion response. Defaulting to empty response. {@Response}", response);
            return new EndnotesFixupResponse
            {
                FixedHtml = html,
            };
        }
    }
}