using System.Text.Json;
using BookAI.Services.Models;
using OpenAI.Chat;
using ChatMessage = OpenAI.Chat.ChatMessage;
using ChatResponseFormat = OpenAI.Chat.ChatResponseFormat;

namespace BookAI.Services;

public class AIService(ChatClient chatClient)
{
    public async Task<ExplanationResponse> ExplainAsync(string sentence, Chunk chunk, CancellationToken cancellationToken)
    {
        var initialRequest = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                 I'm going to send you text, please briefly explain what it is about:
                                 ```
                                 {chunk.Context}

                                 {chunk.Text}
                                 ```
                                 """),
        }, cancellationToken: cancellationToken);
        
        var secondRequest = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                 I'm going to send you text, please briefly explain what it is about:
                                 ```
                                 {chunk.Context}

                                 {chunk.Text}
                                 ```
                                 """),
            new AssistantChatMessage(initialRequest.Value.Content[0].Text),
            new UserChatMessage($"Please explain the following sentence in simple English: '{sentence}'\nYour output will be added as an endnote"),
        }, cancellationToken: cancellationToken);

        return new ExplanationResponse
        {
            Explanation = secondRequest.Value.Content[0].Text,
        };
    }
    public async Task<StraightforwardnessResponse> EvaluateStraightforwardnessAsync(Chunk chunk, CancellationToken cancellationToken)
    {
        var initialRequest = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage($"""
                                 I'm going to send you text, please briefly explain what it is about:
                                 ```
                                 {chunk.Context}

                                 {chunk.Text}
                                 ```

                                 """),
        }, cancellationToken: cancellationToken);

        const string schemaJson = """
                                  {
                                      "required": ["sentenceRatings"],
                                      "type": "object",
                                      "properties": {
                                          "sentenceRatings": {
                                              "type": "array",
                                              "items": {
                                                  "type": "object",
                                                  "properties": {
                                                      "sentence": { "type": "string" },
                                                      "straightforwardness": { "type": "integer" },
                                                      "explanation": { "type": ["string","null"] }
                                                  },
                                                  "required": ["sentence", "straightforwardness", "explanation"],
                                                  "additionalProperties": false
                                              }
                                          }
                                      },
                                      "additionalProperties": false
                                  }
                                  """;


        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("json_schema", BinaryData.FromString(schemaJson), jsonSchemaIsStrict: true);
        
        var response = await chatClient.CompleteChatAsync(new ChatMessage[]
        {
            new SystemChatMessage("You are a text assistant."),
            new UserChatMessage("So, I sent you a text, what's going on?"),
            new AssistantChatMessage(initialRequest.Value.Content[0].Text),
            new UserChatMessage($"""
                            I'm going to send you the text again, please highlight confusing sentences and provide a straightforwardness scores for some of the confusing sentences, which are hard to follow. Explain why are those sentences hard to understand.
                            
                            This is the previously appeared text for you to understand the text:
                            ```
                            {chunk.Context}
                            ```
                            
                            This was the previously appeared text.
                            
                            Current text chunk:
                            ```
                            {chunk.Text}
                            ```

                            For each sentence, if the straightforwardness is 0 it means the sentence is very hard to grasp.
                            If the straightforwardness is 10 the sentence is easy to read and understand.

                            Please return your result as a JSON object with "sentenceRatings" array that has objects with the following keys:
                            - "sentense": (the sentence from the text chunk)
                            - "straightforwardness": (a number between 0 and 10)
                            - "explanation": (the explanation)
                            """)}, new ChatCompletionOptions
        {
            ResponseFormat = chatResponseFormat,
        }, cancellationToken: cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<StraightforwardnessResponse>(response.Value.Content[0].Text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            })!;
        }
        catch
        {
            Console.WriteLine("Something went wrong");
            return null;
        }
    }
}