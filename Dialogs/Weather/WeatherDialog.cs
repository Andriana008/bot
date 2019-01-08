using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
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
                PromptForCityStepAsync,
                DisplayWeatherStateStepAsync,
            };
            AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
         
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
           
            if (string.IsNullOrWhiteSpace(weatherState.City))
            {
                var opts = new PromptOptions
                {
                    Prompt = new Activity
                    {
                        Type = ActivityTypes.Message,
                        Text = $"Hello in which city do you what to know weather?",
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
                await context.SendActivityAsync($"I couldn't find the weather for '{weatherState.City}'.  Are you sure that's a real city?");
                weatherState.City = null;
                return await stepContext.EndDialogAsync();
            }
          
            IMessageActivity message = null;
            
            var result = GetCard(weatherState.City);
            message = stepContext.Context.Activity.AsMessageActivity();
            if (message.Attachments == null)
                message.Attachments = new List<Attachment>();
            var attachment = new Attachment()
            {
                Content = result,
                ContentType = "application/vnd.microsoft.card.adaptive",
                Name = "Weather card"
            };
            message.Attachments.Add(attachment);
           
           
          
            await context.SendActivityAsync(message);

            weatherState.City = null;
            return await stepContext.EndDialogAsync();
            
        }

        private static AdaptiveCard GetCard(string place)
        {
            var client = new OpenWeatherAPI.OpenWeatherAPI("27db98240a74caf542be70bc49ecff4f");
            var results = client.Query(place);
            var card = new AdaptiveCard();
            if (results != null)
            {
                card.Speak = $"<s>Today the temperature is {results.Main.Temperature.CelsiusCurrent} in {place}</s><s>Winds are {results.Wind.SpeedMetersPerSecond} metrs per second from the {results.Wind.Direction}</s>";
                AddCurrentWeather(results, card);
                return card;

            }
            return null;
        }
        private static void AddCurrentWeather(OpenWeatherAPI.Query model, AdaptiveCard card)
        {
            var current = new ColumnSet();
            card.Body.Add(current);

            var currentColumn = new Column();
            current.Columns.Add(currentColumn);
            currentColumn.Size = "35";

            var currentImage = new Image();
            currentColumn.Items.Add(currentImage);
            currentImage.Url = GetIconUrl(model.Weathers[0].Icon);

            var currentColumn2 = new Column();
            current.Columns.Add(currentColumn2);
            currentColumn2.Size = "65";



            AddTextBlock(currentColumn2, $"{model.Name}", TextSize.Large, false);
            AddTextBlock(currentColumn2, $"{model.Main.Temperature.CelsiusCurrent}° C", TextSize.Large);
            AddTextBlock(currentColumn2, $"{model.Weathers[0].Description}", TextSize.Medium);
            AddTextBlock(currentColumn2, $"Winds {model.Wind.SpeedMetersPerSecond} mps {model.Wind.Direction}", TextSize.Medium);
        }
        private static void AddTextBlock(Column column, string text, TextSize size, bool isSubTitle = true)
        {
            column.Items.Add(new TextBlock()
            {
                Text = text,
                Size = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsSubtle = isSubTitle,
                Separation = SeparationStyle.None
            });
        }
        private static string GetIconUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            if (url.StartsWith("http"))
                return url;
            //some clients do not accept \\
            return "http://openweathermap.org/img/w/" + url + ".png";
        }
    }
}
