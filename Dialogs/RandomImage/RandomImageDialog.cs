using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using Unsplasharp;
using Attachment = Microsoft.Bot.Schema.Attachment;

namespace BasicBot.Dialogs.RandomImage
{
    public class RandomImageDialog : ComponentDialog
    {
        private const string ImageStateProperty = "quoteState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";

        public IStatePropertyAccessor<RandomImageState> UserProfileAccessor { get; }

        public RandomImageDialog(IStatePropertyAccessor<RandomImageState> userProfileStateAccessor,
            ILoggerFactory loggerFactory)
            : base(nameof(RandomImageDialog))
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
                        Text = $"Choose categories(nature,water,sun) of image?",
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
            var client = new UnsplasharpClient("197125b8b757915cb7481bbca1167c12e9a65d3b9e43905cafad5beb271eac96");
            var randomPhotosFromQuery =
                await client.GetRandomPhoto(true, count: 1, query: weatherState.categories);
            var randomPhotosFromQuery2 =
                await client.GetRandomPhoto(true, count: 1, query: "cat");
            string x = randomPhotosFromQuery.First().Urls.Full;
            message.Attachments.Add(new Attachment()
            {
                ContentUrl = x,
                ContentType = "image/png",
                Name = "random.png"
            });
            weatherState.categories = null;

            await context.SendActivityAsync(message);

            return await stepContext.EndDialogAsync();

        }

        private async Task<string> GetIconUrl(string url)
        {
            string urll =
                "https://api.unsplash.com/photos/?client_id=197125b8b757915cb7481bbca1167c12e9a65d3b9e43905cafad5beb271eac96&random/?" +
                url;
            var client = new UnsplasharpClient("197125b8b757915cb7481bbca1167c12e9a65d3b9e43905cafad5beb271eac96");
            var randomPhotosFromQuery = await client.GetRandomPhoto(count: 1, query: "nature");



            if (string.IsNullOrEmpty(url))
                return string.Empty;

            //some clients do not accept \\
            return randomPhotosFromQuery.First().Urls.Full + ".png";
        }

        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context, () => null);
            if (weatherState == null)
            {
                var weatherStateOpt = stepContext.Options as RandomImageState;
                if (weatherStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new RandomImageState());
                }
            }

            return await stepContext.NextAsync();
        }

    }
}


