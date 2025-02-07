using System.Text.Json;
using BookAI.Services.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace BookAI.Services;

public class AIService(ChatClient chatClient, ILogger<AIService> logger)
{
    public async Task<ExplanationResponse> ExplainAsync(string sentence, Chunk chunk, CancellationToken cancellationToken)
    {
        logger.LogDebug("Sending explanation initial request");

        var initialRequest = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                 I'm going to send you text, please briefly explain what it is about so I can easily understand:
                                 ```
                                 {chunk.Context}

                                 {chunk.Text}
                                 ```
                                 
                                 Specifically the sentence '{sentence}' seems a little confusing.
                                 """)
        }, cancellationToken: cancellationToken);

        logger.LogDebug("Sending explanation secondary request");

        var secondRequest = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new AssistantChatMessage(initialRequest.Value.Content[0].Text),
            new UserChatMessage($"Based on the above explanation, please provide a simple footnote explanation for the following sentence: '{sentence}'\nYour output will be placed as a footnote.\nFootnote:")
        }, cancellationToken: cancellationToken);

        var explanation = secondRequest.Value.Content[0].Text.Trim().Trim(StringComparison.OrdinalIgnoreCase, "Footnote:", "**Footnote**", "*Footnote*", ":", "**Footnote:**", "*Footnote:*").Trim();

        return new ExplanationResponse
        {
            Explanation = explanation
        };
    }

    public async Task<ConfusionResponse> EvaluateConfusionAsync(Chunk chunk, CancellationToken cancellationToken)
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
                                 I'm going to send you text chunk. Please briefly explain what it is about and then highlight confusing sentences and provide confusion scores for some of the confusing ones, which are hard to follow or understand.

                                 Previously appeared text for you to understand the text:
                                 ```
                                 {chunk.Context}
                                 ```

                                 Current text chunk:
                                 ```
                                 {chunk.Text}
                                 ```

                                 For each sentence or text part in current text chunk, if the confusion is 10 then it is very hard to grasp.
                                 For each sentence or text part in current text chunk, if the confusion is 0 then is easy to read and understand.

                                 Please return your result as a JSON object with "textConfusionScores" array that has objects with the following keys:
                                 - "text": (the part from the text chunk that is confusing, can be a sentence or any part really)
                                 - "confusionScore": (a number from 0 to 10)
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
}