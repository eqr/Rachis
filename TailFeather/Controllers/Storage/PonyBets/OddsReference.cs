namespace TailFeather.Storage.PonyBets
{
    using System.Collections.Generic;

    public class OddsReference
    {
        public static List<Odd> AvailableOdds
        {
            get
            {
                return new List<Odd>()
                           {
                               new Odd
                                   {
                                       OddId = "odds/1",
                                       PonyId = "ponies/1",
                                       OddValue = "1/6",
                                       RaceId = "races/1"
                                   }
                           };
            }
        }
    }
}