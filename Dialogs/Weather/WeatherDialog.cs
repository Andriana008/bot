using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;

namespace BasicBot.Dialogs.Weather
{
    public class WeatherDialog : ComponentDialog
    {
        private const string WeatherStateProperty = "weatherState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";
        public IStatePropertyAccessor<WeatherState> UserProfileAccessor { get; }

        public WeatherDialog(IStatePropertyAccessor<WeatherState> userProfileStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(WeatherDialog))
        {
            UserProfileAccessor = userProfileStateAccessor ?? throw new ArgumentNullException(nameof(userProfileStateAccessor));

            // Add control flow dialogs
            var waterfallSteps = new WaterfallStep[]
            {
                InitializeStateStepAsync,
                //PromptForNameStepAsync,
                PromptForCityStepAsync,
                DisplayWeatherStateStepAsync,
            };
            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
           // AddDialog(new TextPrompt(NamePrompt, ValidateName));
            AddDialog(new TextPrompt(CityPrompt, ValidateCity));
        }
        private async Task<DialogTurnResult> InitializeStateStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context, () => null);
            if (weatherState == null)
            {
                var weatherStateOpt = stepContext.Options as WeatherState;
                if (weatherStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new WeatherState());
                }
            }

            return await stepContext.NextAsync();
        }
        private async Task<DialogTurnResult> PromptForCityStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Save name, if prompted.
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);
            //var lowerCaseName = stepContext.Result as string;
            //if (string.IsNullOrWhiteSpace(weatherState.Name) && lowerCaseName != null)
            //{
            //    // Capitalize and set name.
            //    weatherState.Name = char.ToUpper(lowerCaseName[0]) + lowerCaseName.Substring(1);
            //    await UserProfileAccessor.SetAsync(stepContext.Context, weatherState);
            //}

            if (string.IsNullOrWhiteSpace(weatherState.City))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"Hello what city do you what to know weather?",
                    },
                };
                return await stepContext.PromptAsync(CityPrompt, opts);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private const int CityLengthMinValue = 4;
        private async Task<bool> ValidateCity(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            // Validate that the user entered a minimum lenght for their name
            var value = promptContext.Recognized.Value?.Trim() ?? string.Empty;
            if (value.Length >= CityLengthMinValue)
            {
                promptContext.Recognized.Value = value;
                return true;
            }
            else
            {
                await promptContext.Context.SendActivityAsync($"City names needs to be at least `{CityLengthMinValue}` characters long.");
                return false;
            }
        }

        private async Task<DialogTurnResult> DisplayWeatherStateStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            // Save city, if prompted.
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);

            var lowerCaseCity = stepContext.Result as string;
            if (string.IsNullOrWhiteSpace(weatherState.City) &&
                !string.IsNullOrWhiteSpace(lowerCaseCity))
            {
                // capitalize and set city
                weatherState.City = char.ToUpper(lowerCaseCity[0]) + lowerCaseCity.Substring(1);
                await UserProfileAccessor.SetAsync(stepContext.Context, weatherState);
            }

            return await weatherUser(stepContext);
        }

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> weatherUser(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
            var weatherState = await UserProfileAccessor.GetAsync(context);

            var client = new OpenWeatherAPI.OpenWeatherAPI("27db98240a74caf542be70bc49ecff4f");

            var results = client.Query(weatherState.City);
            if (results == null)
            {
                await context.SendActivityAsync($"No city");
                weatherState.City = null;
                return await stepContext.EndDialogAsync();
            }
            //var message = stepContext.Context.Activity.AsMessageActivity();
            //Attachment attachment = new Attachment();
            //attachment.ContentType = "image/png";
            //attachment.ContentUrl = GetIconUrl(results.Weathers[0].Icon);
            //message.Attachments.Add(attachment);
            //message.Text =
            //        $"The temperature in {weatherState.City} is {results.Main.Temperature.CelsiusCurrent}C and {results.Main.Temperature.FahrenheitCurrent}F." +
            //        $" There is {results.Wind.SpeedFeetPerSecond} f/s wind in the {results.Wind.Direction} direction.";
            await context.SendActivityAsync($"The temperature in {weatherState.City} is {results.Main.Temperature.CelsiusCurrent}C and {results.Main.Temperature.FahrenheitCurrent}F." +
                                                        $" There is {results.Wind.SpeedFeetPerSecond} f/s wind in the {results.Wind.Direction} direction.");
            // Display their profile information and end dialog.

            weatherState.City = null;
            return await stepContext.EndDialogAsync();
            
        }
        private static string GetIconUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            if (url.StartsWith("http"))
                return url;
            //some clients do not accept \\
            return "https://cdn.apixu.com/weather/64x64/day/" + url;
        }
    }
}
