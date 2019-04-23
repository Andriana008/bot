using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using NewsAPI;
using NewsAPI.Constants;
using NewsAPI.Models;

namespace BasicBot.Dialogs.News
{
    public class NewsDialog : ComponentDialog
    {
        private const string ImageStateProperty = "quoteState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";

        public IStatePropertyAccessor<NewsState> UserProfileAccessor { get; }

        public NewsDialog(IStatePropertyAccessor<NewsState> userProfileStateAccessor,
            ILoggerFactory loggerFactory)
            : base(nameof(NewsDialog))
        {
            UserProfileAccessor = userProfileStateAccessor ??
                                  throw new ArgumentNullException(nameof(userProfileStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                PromptForCityStepAsync,
                DisplayWeatherStateStepAsync
            };

            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
            AddDialog(new TextPrompt(CityPrompt, ValidateCity));
        }

        private async Task<bool> ValidateCity(PromptValidatorContext<string> promptContext,
            CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum lenght for their name
            return true;
        }

        private async Task<DialogTurnResult> PromptForCityStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Save name, if prompted.
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);

            if (string.IsNullOrWhiteSpace(weatherState.categories))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"What news are you looking for?",
                    },
                };
                return await stepContext.PromptAsync(CityPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }

        }



        private async Task<DialogTurnResult> DisplayWeatherStateStepAsync(
           WaterfallStepContext stepContext,
           CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(weatherState.categories) &&
                !string.IsNullOrWhiteSpace(lowerCaseCity))
            {
                // capitalize and set city
                weatherState.categories = char.ToUpper(lowerCaseCity[0]) + lowerCaseCity.Substring(1);
                await UserProfileAccessor.SetAsync(stepContext.Context, weatherState);
            }

            return await weatherUser(stepContext);
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> weatherUser(WaterfallStepContext stepContext)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);
            var context = stepContext.Context;
            IMessageActivity message = stepContext.Context.Activity.AsMessageActivity();
            if (message.Attachments == null)
                message.Attachments = new List<Attachment>();
            //string url = await GetIconUrl(weatherState.categories);
            var newsApiClient = new NewsApiClient("8763580b353d4001ac2debdc9541c633");
            var articlesResponse = newsApiClient.GetEverything(new EverythingRequest
            {
                Q =weatherState.categories,
                SortBy = SortBys.Popularity,
                Language = Languages.EN
            });
            if (articlesResponse.Status == Statuses.Ok)
            {
                // total results found
                var thumbnailCard1 = new ThumbnailCard
                {
                    Text = "Results found:"+$"{articlesResponse.TotalResults}",
                };
                var thumbnailCard2 = new ThumbnailCard
                {
                    Text = "Top-5",
                };
                message.Attachments.Add(thumbnailCard1.ToAttachment());
                message.Attachments.Add(thumbnailCard2.ToAttachment());
                var top=articlesResponse.Articles.GetRange(0, 5);
                // here's the first 20
                foreach (var article in top)
                {
                    // title
                    Console.WriteLine(article.Title);
                    // author
                    Console.WriteLine(article.Author);
                    // description
                    Console.WriteLine(article.Description);
                    // url
                    Console.WriteLine(article.Url);
                    // image
                    Console.WriteLine(article.UrlToImage);
                    // published at
                    Console.WriteLine(article.PublishedAt);
                    var thumbnailCard = new ThumbnailCard
                    {
                        Title = article.Title,
                        Text = article.Url,
                    };
                    message.Attachments.Add(thumbnailCard.ToAttachment());
                }
            }
            else
            {
                var thumbnailCard = new ThumbnailCard
                {
                    Text = "No results"
                };
                message.Attachments.Add(thumbnailCard.ToAttachment());
            }
            weatherState.categories = null;

            await context.SendActivityAsync(message);

            return await stepContext.EndDialogAsync();

        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context, () => null);
            if (weatherState == null)
            {
                var weatherStateOpt = stepContext.Options as NewsState;
                if (weatherStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new NewsState());
                }
            }

            return await stepContext.NextAsync();
        }
    }
}
