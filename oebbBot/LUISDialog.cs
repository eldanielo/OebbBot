using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;

using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System.Net;

using Newtonsoft.Json.Linq;

namespace oebbBot
{
    [Serializable]

    public class LUISDialog : IDialog<object>
    {
        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }


        public const string Destination = "Destination";

        public const string Start = "Start";

        public const string Datetime = "builtin.datetime.time";
        [Serializable]

        public class Result {
           public string name;
            public IEnumerable<Entity> list;
            public int count = 0;
        }
      
      


        private static async Task<LUISModel> GetEntityFromLUIS(string Query)
        {
            Query = Uri.EscapeDataString(Query);
            LUISModel Data = new LUISModel();
            using (HttpClient client = new HttpClient())
            {
                string RequestURI = "https://api.projectoxford.ai/luis/v1/application?id=66a24e48-9e4d-4021-866f-a7e789b5c0a8&subscription-key=40a189ba4f8a4824b4e3371ca059a82b&q=" + Query;
                HttpResponseMessage msg = await client.GetAsync(RequestURI);

                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = await msg.Content.ReadAsStringAsync();
                    Data = JsonConvert.DeserializeObject<LUISModel>(JsonDataResponse);
                }
            }
            return Data;
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<Message> argument)
        {
            var message = await argument;

            LUISModel model = await GetEntityFromLUIS(message.Text);

            if (model.intents.Count() > 0)
            {
                switch (model.intents[0].intent)
                {
                    case "hi":

                        await context.PostAsync("Hallo! Try: \n: I want to book a train from vienna to salzburg tomorrow at 5");
                        context.Wait(MessageReceivedAsync);
                        break;
                    case "GoTo":

                        if (model.entities.Count() > 0)
                        {

                            string dest, start;
                            DateTime departureTime = DateTime.Now;

                            //GET DESTINATION
                            var hasDest = model.entities.FirstOrDefault(e => e.type == Destination);
                            if (hasDest == null)
                            {
                                dest = "";
                                PostAndWait(context, "please provide a Destination");
                            }
                            else
                            {
                                dest = hasDest.entity;
                            }

                            //GET START
                            var hasStart = model.entities.FirstOrDefault(e => e.type == Start);
                            if (hasStart == null)
                            {
                                start = getCity();
                            }
                            else
                            {
                                start = hasStart.entity;
                            }


                        
         

                            //GET Date
                            var hasDate = model.entities.Where(e => e.type == "builtin.datetime");
                            if (hasDate != null && hasDate.Count() > 0) {
                                var date = hasDate.ElementAt(0).entity;
                                string time = "";
                                if (hasDate.Count() > 1) {
                                    time = hasDate.ElementAt(1).entity;
                                }
                               
                                var parser = new Chronic.Parser();

                                var span = parser.Parse(date  + " " + time );
                                if (span != null)
                                {
                                    var when = span.Start ?? span.End;
                                    departureTime = when.Value;
                                }
                            }

                         
                       



                            Result person = new Result { name = "Person", count=1 };
                           Result vorteilscard = new Result { name = "vorteilscard" };
                            Result car = new Result { name = "vehicle::car" };
                            Result motorcycle = new Result { name = "vehicle::motorcycle" };
                           Result bicycle = new Result { name = "vehicle::bicycle" };

                            List<Result> results = new List<Result>
                            { person, vorteilscard, car, motorcycle,bicycle};
                            foreach (var r in results)
                            {                    
                                GetResult(model, r);
                            }

                         
                            //GET NUMBERS
                            var hasNumber = model.entities.Where(e => e.type == "Number");
                            Dictionary<int, int> numberAt = new Dictionary<int, int>();
                            if (hasNumber != null)
                            {
                                foreach (var num in hasNumber)
                                {
                                    numberAt.Add(num.startIndex, int.Parse(num.entity));
                                }
                            }


                            Dictionary<Result, int> dict = new Dictionary<Result, int>();
                            foreach (var n in numberAt)
                            {
                                dict.Clear();
                                foreach (var r in results)
                                {
                                    dict.Add(r, int.MaxValue);
                                }

                                foreach (var d in results)
                                {
                                    foreach (var r in d.list)
                                    {
                                        if (n.Key > r.startIndex)
                                            continue;
                                        var x = Math.Abs(n.Key - r.startIndex);
                                        if (x < dict[d])
                                        {
                                            dict[d] = x;
                                        }
                                    }
                                }

                                //set count  for appropiate entity
                                int closest = int.MaxValue;
                                Result closestResult = null;
                                foreach (var x in dict)
                                {
                                    if (x.Value < closest)
                                    {
                                        closest = x.Value;
                                        closestResult = x.Key;
                                    }
                                }
                                if (closestResult == null)
                                {
                                    person.count += n.Value -1;
                                }
                                else
                                {

                                    closestResult.count += n.Value - 1;
                                }
                            }
                            if (model.query.Contains("all")) {
                                vorteilscard.count = person.count;
                            }
                            context.PerUserInConversationData.SetValue<DateTime>("departure", departureTime);
                            context.PerUserInConversationData.SetValue<String>("destination", dest);
                            context.PerUserInConversationData.SetValue<String>("start", start);

                            MakeDialog(context, dest, start, departureTime, person, vorteilscard,car, motorcycle, bicycle);
                        }
                        break;
                    default:
                        PostAndWait(context, "Sorry, didn't get that");
                        break;
                }
            }
        }

        private Result GetResult(LUISModel model, Result result)
        {
            result.list = model.entities.Where(e => e.type == result.name);
            if (result.list.Count() > 0)
            {
                result.count = result.list.Count();
            }
            return result;
        }

        private void MakeDialog(IDialogContext context, string dest, string start, DateTime departureTime, Result person, Result vorteil, Result car, Result motorcycle, Result bicycle)
        {
        
            PromptDialog.Choice<string>(
                       context,
                      ConfirmAsync, new[] { "1", "2", "sooner", "later", "None" }, "I found 2 trains close to your dates from "
                      + start + " to " + dest + " for " + person.count + " people with:" + MakeString(vorteil) + MakeString(car) + MakeString(motorcycle) + MakeString(bicycle)
                      + "\n\n**1. train: ** " + getNextXHour(departureTime, 1).ToString("dd.M HH:mm")
                      + "\n\n**2. train: ** " + getNextXHour(departureTime, 2).ToString("dd.M HH:mm")
                      + ". \n \nSay **1**/**2**/**sooner**/**later**/**none**", "Say 1, 2, sooner, later or none");
        }

        private static string MakeString(Result result)
        {
            return result.count > 0 ? "\n\n* " + result.count + " "  + result.name.Replace("vehicle::","") +"s ": "";
        }

        public DateTime getNextXHour(DateTime time, int x) {
           time =  time.Subtract(new TimeSpan(0, 0, 1));
            var nextFullHour= new TimeSpan(x, 0, 0).Subtract(new TimeSpan(0,time.Minute,0)).Subtract(new TimeSpan(0,0,time.Second));
            DateTime departure = time.Add(nextFullHour);
            return departure;
        }


        private async Task ConfirmAsync(IDialogContext context, IAwaitable<string> result)
        {
            var confirm = await result;
                     DateTime departureTime;
            if (context.PerUserInConversationData.TryGetValue<DateTime>("departure", out departureTime)) ;
            switch (confirm)
            {

                case "1":
                    await context.PostAsync("1. Gebucht! Gute Reise");
                    break;
                case "2":
                    await context.PostAsync("2. Gebucht! Gute Reise");
                    break;
                case "sooner":
                    if (departureTime.Subtract(new TimeSpan(1, 0, 0)) < DateTime.Now) {
                        await context.PostAsync("Earlier trains already departed");
                    }
                    PromptDialog.Choice<string>(
                    context,
                   ConfirmAsync, new[] { "1", "2", "sooner", "later", "None" }, "Here are two earlier options: \n\n**1. train**" + departureTime.Subtract(new TimeSpan(1,0,0)).ToString("dd.M HH:mm") + "\n\n**2. train** " + departureTime.Subtract(new TimeSpan(2, 0, 0)).ToString("dd.M HH:mm") + "\n\nSay **1**/**2**/**None**", "Say **1**, **2** or **None**");
                    return;
                    
                case "later":

                    PromptDialog.Choice<string>(
                       context,
                      ConfirmAsync, new[] { "1", "2", "sooner", "later", "None" }, "Here are two later options: \n\n**1. train**" + getNextXHour(departureTime, 3).ToString("dd.M HH:mm") + "\n\n**2. train**" + getNextXHour(departureTime, 4).ToString("dd.M HH:mm") + "\n\nSay **1**/**2**/**None**", "Say **1**, **2** or **None**");
                    return;
                    
                case "None":
                    await context.PostAsync("Vorgang abgebrochen");
                    break;
            }
            context.Wait(MessageReceivedAsync);

        }

        public static string getCity()
        {
            //2da7d59b916ec038bdb243d2adf389f4958d5f8e9fe8cf6fb838d72cef829bbf

            string s = new WebClient().DownloadString("http://api.ipinfodb.com/v3/ip-city/?key=2da7d59b916ec038bdb243d2adf389f4958d5f8e9fe8cf6fb838d72cef829bbf&format=json");
            dynamic location = JObject.Parse(s);
            return location.cityName;
        }
        private async void PostAndWait(IDialogContext context, string resp)
        {

            await context.PostAsync(resp);

            context.Wait(MessageReceivedAsync);
        }





    } }