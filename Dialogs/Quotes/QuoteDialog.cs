using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Unsplasharp;

namespace BasicBot.Dialogs.Quotes
{
    public class QuoteDialog : ComponentDialog
    {
        private const string ImageStateProperty = "quoteState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";

        public IStatePropertyAccessor<QuoteState> UserProfileAccessor { get; }

        public QuoteDialog(IStatePropertyAccessor<QuoteState> userProfileStateAccessor,
            ILoggerFactory loggerFactory)
            : base(nameof(QuoteDialog))
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
                        Text = $"Do you whant some programming quote?",
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
          
            
            //string url = await GetIconUrl(weatherState.categories);
            IMessageActivity message = null;
            if (weatherState.categories.ToLower() == "yes")
            {
                message = await GetProgrammer(stepContext, new AdaptiveCards.AdaptiveCard(), "Quote card");
                if (message.Attachments == null)
                    message.Attachments = new List<Attachment>();
                await context.SendActivityAsync(message);
                weatherState.categories = null;
                return await stepContext.EndDialogAsync();
            }

            weatherState.categories = null;

            await context.SendActivityAsync("other categories will be realised soon");

            return await stepContext.EndDialogAsync();

        }

      
        private async Task<IMessageActivity> GetProgrammer(WaterfallStepContext stepContext, AdaptiveCards.AdaptiveCard card, string cardName)
        {
            string urll =
                "http://quotes.stormconsultancy.co.uk/random.json";
            var message = stepContext.Context.Activity.AsMessageActivity();
            JObject jsonData = JObject.Parse(new System.Net.WebClient().DownloadString(urll));
            string t= jsonData.SelectToken("author").ToString();
            string a= jsonData.SelectToken("quote").ToString();
            var thumbnailCard = new ThumbnailCard
            {
                Title = t,
                Text = a,

            };
            if (message.Attachments == null)
                message.Attachments = new List<Attachment>();
            message.Attachments.Add(thumbnailCard.ToAttachment());
            return message;
        }
        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context, () => null);
            if (weatherState == null)
            {
                var weatherStateOpt = stepContext.Options as QuoteState;
                if (weatherStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new QuoteState());
                }
            }

            return await stepContext.NextAsync();
        }
    }
}
