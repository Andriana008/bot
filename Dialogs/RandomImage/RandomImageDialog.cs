using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BasicBot.Dialogs.RandomImage
{
    public class RandomImageDialog : ComponentDialog
    {
        private const string ImageStateProperty = "quoteState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";

        public IStatePropertyAccessor<RandomImageState> UserProfileAccessor { get; }

        public RandomImageDialog(IStatePropertyAccessor<RandomImageState> userProfileStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(RandomImageDialog))
        {
            UserProfileAccessor = userProfileStateAccessor ?? throw new ArgumentNullException(nameof(userProfileStateAccessor));

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
        private async Task<bool> ValidateCity(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
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
                        Text = $"Hello,whose categories(nature,water,sun)?",
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
            var context = stepContext.Context;
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);
            IMessageActivity message = stepContext.Context.Activity.AsMessageActivity();
            if (message.Attachments == null)
                message.Attachments = new List<Attachment>();
            string url = GetIconUrl(weatherState.categories);
            message.Attachments.Add(new Attachment()
            {
                ContentUrl = url,
                ContentType = "image/png",
                Name = "random.png"
            });
            weatherState.categories = null;

            await context.SendActivityAsync(message);

            return await stepContext.EndDialogAsync();
        }

        private static string GetIconUrl(string url)
        {
            string urll = "https://api.unsplash.com/photos/?client_id=197125b8b757915cb7481bbca1167c12e9a65d3b9e43905cafad5beb271eac96&random/?" + url;

            string baseStr = "";
            try
            {
                string jsonResult = new System.Net.WebClient().DownloadString(urll);
                jsonResult = jsonResult.TrimStart(new char[] { '[' }).TrimEnd(new char[] { ']' });
                JObject jsonData = JObject.Parse(jsonResult);

                if (jsonData.SelectToken("id").ToString() != null)
                {

                    baseStr = jsonData.SelectToken("full").ToString();
                }
            }
            catch (Exception ex)
            {

            }
        
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            //some clients do not accept \\
            return baseStr+".png";
        }
        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
