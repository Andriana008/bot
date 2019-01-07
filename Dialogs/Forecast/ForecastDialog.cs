using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using APIXULib;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using AdaptiveCards;

namespace BasicBot.Dialogs.Forecast
{
    public class ForecastDialog : ComponentDialog
    {
        private const string WeatherStateProperty = "weatherState";

        private const string CityValue = "weatherCity";
        private const string CityPrompt = "cityPrompt";
        private const string ProfileDialog = "profileDialog";
        public IStatePropertyAccessor<ForecastState> UserProfileAccessor { get; }

        public ForecastDialog(IStatePropertyAccessor<ForecastState> userProfileStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(ForecastDialog))
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
                var weatherStateOpt = stepContext.Options as ForecastState;
                if (weatherStateOpt != null)
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, weatherStateOpt);
                }
                else
                {
                    await UserProfileAccessor.SetAsync(stepContext.Context, new ForecastState());
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
                        Text = $"Hello for which city do you what to get forecast?",
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


        private static AdaptiveCard GetCard(string place)
        {
            WeatherModel model = new Repository().GetWeatherData("5598b2a277d04bf69ee112411190401", GetBy.CityName, place, Days.Five);

            var card = new AdaptiveCard();
            if (model != null)
            {
                if (model.current != null)
                {
                    card.Speak = $"<s>Today the temperature is {model.current.temp_c} in {model.location.name}</s><s>Winds are {model.current.wind_mph} miles per hour from the {model.current.wind_dir}</s>";
                }

                if (model.forecast != null && model.forecast.forecastday != null)
                {
                    AddCurrentWeather(model, card);
                    AddForecast(place, model, card);
                    return card;
                }
            }
            return null;
        }
        private static void AddCurrentWeather(WeatherModel model, AdaptiveCard card)
        {
            var current = new ColumnSet();
            card.Body.Add(current);

            var currentColumn = new Column();
            current.Columns.Add(currentColumn);
            currentColumn.Size = "35";

            var currentImage = new Image();
            currentColumn.Items.Add(currentImage);
            currentImage.Url = GetIconUrl(model.current.condition.icon);

            var currentColumn2 = new Column();
            current.Columns.Add(currentColumn2);
            currentColumn2.Size = "65";

            string date = DateTime.Parse(model.current.last_updated).DayOfWeek.ToString();

            AddTextBlock(currentColumn2, $"{model.location.name} ({date})", TextSize.Large, false);
            AddTextBlock(currentColumn2, $"{model.current.temp_c.ToString().Split('.')[0]}° C", TextSize.Large);
            AddTextBlock(currentColumn2, $"{model.current.condition.text}", TextSize.Medium);
            AddTextBlock(currentColumn2, $"Winds {model.current.wind_mph} mph {model.current.wind_dir}", TextSize.Medium);
        }
        private static void AddForecast(string place, WeatherModel model, AdaptiveCard card)
        {
            var forecast = new ColumnSet();
            card.Body.Add(forecast);

            foreach (var day in model.forecast.forecastday)
            {
                if (DateTime.Parse(day.date).DayOfWeek != DateTime.Parse(model.current.last_updated).DayOfWeek)
                {
                    var column = new Column();
                    AddForcastColumn(forecast, column, place);
                    AddTextBlock(column, DateTimeOffset.Parse(day.date).DayOfWeek.ToString().Substring(0, 3), TextSize.Medium);
                    AddImageColumn(day, column);
                    AddTextBlock(column, $"{day.day.mintemp_c.ToString().Split('.')[0]}/{day.day.maxtemp_c.ToString().Split('.')[0]}", TextSize.Medium);
                }
            }
        }
        private static void AddImageColumn(Forecastday day, Column column)
        {
            var image = new Image();
            image.Size = ImageSize.Auto;
            image.Url = GetIconUrl(day.day.condition.icon);
            column.Items.Add(image);
        }
        private static string GetIconUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            if (url.StartsWith("http"))
                return url;
            //some clients do not accept \\
            return "https:" + url;
        }
        private static void AddForcastColumn(ColumnSet forecast, Column column, string place)
        {
            forecast.Columns.Add(column);
            column.Size = "20";
            var action = new OpenUrlAction();
            action.Url = $"https://www.bing.com/search?q=forecast in {place}";
            column.SelectAction = action;
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

        // Helper function to greet user with information in GreetingState.
        private async Task<DialogTurnResult> weatherUser(WaterfallStepContext stepContext)
        {
            var context = stepContext.Context;
           
            IMessageActivity message = null;
            message = await GetMessage(stepContext, new AdaptiveCards.AdaptiveCard(), "Weather card");

            if (message == null)
            {
                message = context.Activity.AsMessageActivity();
                message.Text = $"I couldn't find the weather for '{context.Activity.AsMessageActivity().Text}'.  Are you sure that's a real city?";
            }

            await context.SendActivityAsync(message);
            return await stepContext.EndDialogAsync();
        }
        private async Task<IMessageActivity> GetMessage(WaterfallStepContext stepContext,AdaptiveCards.AdaptiveCard card, string cardName)
        {
            var weatherState = await UserProfileAccessor.GetAsync(stepContext.Context);
            var result = GetCard(weatherState.City);
            var message = stepContext.Context.Activity.AsMessageActivity();
            if (message.Attachments == null)
                message.Attachments = new List<Attachment>();
            var attachment = new Attachment()
            {
                Content = result,
                ContentType = "application/vnd.microsoft.card.adaptive",
                Name = cardName
            };
            message.Attachments.Add(attachment);
            return message;
        }
    }
}
