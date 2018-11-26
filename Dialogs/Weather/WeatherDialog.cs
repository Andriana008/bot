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
        public IStatePropertyAccessor<WeatherState> UserProfileAccessor { get; }

        public WeatherDialog(IStatePropertyAccessor<WeatherState> userProfileStateAccessor, ILoggerFactory loggerFactory)
            : base(nameof(WeatherDialog))
        {
            UserProfileAccessor = userProfileStateAccessor ?? throw new ArgumentNullException(nameof(userProfileStateAccessor));

            //// Add control flow dialogs
            //var waterfallSteps = new WaterfallStep[]
            //{
            //    InitializeStateStepAsync,
            //    PromptForNameStepAsync,
            //    PromptForCityStepAsync,
            //    DisplayGreetingStateStepAsync,
            //};
            //AddDialog(new WaterfallDialog(ProfileDialog, waterfallSteps));
            //AddDialog(new TextPrompt(NamePrompt, ValidateName));
            //AddDialog(new TextPrompt(CityPrompt, ValidateCity));
        }
    }
}
