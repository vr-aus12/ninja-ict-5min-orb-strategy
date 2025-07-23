
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using System.Windows.Media;
using NinjaTrader.NinjaScript.DrawingTools;  // <--- Required for Draw methods
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;
using NinjaTrader.Gui;
using System.ComponentModel.DataAnnotations;



namespace NinjaTrader.NinjaScript.Strategies
{
    public class ICT_ORB_FVG_Visuals : Strategy
    {
        private double openingHigh;
        private double openingLow;
        private double openingClose;
        private bool openingCaptured;
        private bool tradePlaced;
        private DateTime sessionDate;
        private double trailingStopPrice = double.NaN;
        private int swingStrength = 3;
		private int entryBar = -1;
		private string trailingStopTag = "TrailingStop";
		private double entryPrice;
		private double initialRisk;
		private double currentTP;
		
		#region Variables
private double riskPercent = 1; // default to 1%
#endregion

[NinjaScriptProperty]
[Range(0.1, 100)]
[Display(Name = "Risk % of Account", Description = "Risk percentage of account per trade", GroupName = "Risk Settings", Order = 1)]
public double RiskPercent
{
    get { return riskPercent; }
    set { riskPercent = value; }
}




        private readonly TimeSpan ORStartLocal = new TimeSpan(23, 30, 0);
        private readonly TimeSpan OREndLocal = new TimeSpan(23, 35, 0);

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "ICT_ORB_FVG_Visuals";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 60;
                IncludeCommission = true;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, 5);
            }
        }

		private DateTime ConvertToNewYorkTime(DateTime localTime)
		{
    		// Assuming chart time is AEST (UTC+10 or +11 with DST)
    		TimeZoneInfo nyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Windows
    		return TimeZoneInfo.ConvertTimeFromUtc(localTime.ToUniversalTime(), nyTimeZone);
		}

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < 5 || CurrentBars[1] < 1)
                return;

            DateTime currentBarTime = Times[0][0];
            TimeSpan currentLocalTime = currentBarTime.TimeOfDay;
			string timestamp = currentBarTime.ToString("yyyyMMddHHmmss");


            DateTime nyTime = ConvertToNewYorkTime(currentBarTime);
			TimeSpan nyLocalTime = nyTime.TimeOfDay;

			DateTime nySessionDate = nyTime.Date;
			if (nyLocalTime < ORStartLocal)
    			nySessionDate = nySessionDate.AddDays(-1);

            if (sessionDate != nySessionDate)
            {
                openingCaptured = false;
                tradePlaced = false;
                trailingStopPrice = double.NaN;
                sessionDate = nySessionDate;
            }
			
			//Print($"[DEBUG] BarTime={currentBarTime} | TimeOfDay={currentLocalTime} | ORCaptured={openingCaptured} | TradePlaced={tradePlaced} nySessionDate={nySessionDate} sessionDate={sessionDate} nyTime={nyTime}");


            if (BarsInProgress == 1 && !openingCaptured)
            {
                DateTime barTime5Min = Times[1][0];
                TimeSpan barLocalTime5Min = barTime5Min.TimeOfDay;

                //if (barLocalTime5Min >= ORStartLocal && barLocalTime5Min < OREndLocal)
				 if (barLocalTime5Min == OREndLocal)
                {
                    openingHigh = Highs[1][0];
                    openingLow = Lows[1][0];
                    openingClose = Closes[1][0];
                    openingCaptured = true;

                    string tag = "ORBox" + sessionDate.ToString("yyyyMMdd");
                    Draw.Rectangle(this, tag, false, 5, openingHigh, 0, openingLow, Brushes.LightBlue, Brushes.Blue, 20);
                }
            }

            if (BarsInProgress == 0 && openingCaptured && !tradePlaced)
            {
                if (currentLocalTime < OREndLocal)
                    return;

				
				bool bullishFVG = High[2] < Low[0] && Close[2] > Open[2];
				bool bearishFVG = Low[2] > High[0] && Close[2] < Open[2];
				
				// === FVG Visuals ===

    if (bullishFVG)
    {
        string tag = "BullFVG_" + Time[0].ToString("yyyyMMddHHmmss");
        Draw.Rectangle(this, tag, false,
            2, Low[0], 0, High[2],
            Brushes.LightGreen, Brushes.Green, 20);
    }

    if (bearishFVG)
    {
        string tag = "BearFVG_" + Time[0].ToString("yyyyMMddHHmmss");
        Draw.Rectangle(this, tag, false,
            2, High[0], 0, Low[2],
            Brushes.Pink, Brushes.Red, 20);
    }



                double risk = Math.Abs(Close[0] - Open[3]);
                if (risk < TickSize)
                    return;
				
// === Risk Calculation (in dollar terms) ===
double tickSize = TickSize;          
double tickValue = Instrument.MasterInstrument.TickSize * Instrument.MasterInstrument.PointValue;
double accountBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
double maxAllowedRisk = RiskPercent  * accountBalance; 

// Calculate stop distance in points
double stopDistancePoints = Math.Abs(Close[0] - Open[3]);

// Convert to dollar risk
double stopDistanceTicks = stopDistancePoints / tickSize;
double dollarRiskPerContract = stopDistanceTicks * tickValue;



                double reward = 2 * risk;
				int endBarIndex = Bars.GetBar(Time[0].AddMinutes(10));


                if (Close[0] > openingHigh && bullishFVG)
                {
if (dollarRiskPerContract > maxAllowedRisk)
{
    Print($"[RISK BLOCKED] Risk ${dollarRiskPerContract:F2} > Max Allowed ${maxAllowedRisk:F2}");
    return;
}
                   entryPrice = Close[0];
initialRisk = Math.Abs(entryPrice - Open[2]);
currentTP = entryPrice + 2 * initialRisk;

EnterLong("ORB_Long" + timestamp);
SetStopLoss("ORB_Long" + timestamp, CalculationMode.Price, Open[2], false);
SetProfitTarget("ORB_Long" + timestamp, CalculationMode.Price, currentTP);

                    trailingStopPrice = openingClose;
                    tradePlaced = true;
					   entryBar = CurrentBar;  // store the bar index of entry


                    Draw.ArrowUp(this, "LongEntry" + CurrentBar, false, 0, Low[0] - 2 * TickSize, Brushes.Green);

					
					    // Draw limited length lines for SL and TP for now with 10 bars length (for example)
					Draw.Line(this, "Entry_Line"+timestamp, false,  0,Close[0], 5, Close[0], Brushes.Blue, DashStyleHelper.Dash, 2);
    				Draw.Line(this, "SL_Long_Line"+timestamp, false,  0,Open[2], 5, Open[2], Brushes.Red, DashStyleHelper.Dash, 2);
    				Draw.Line(this, "TP_Long_Line"+timestamp, false, 0,Close[0] + reward, 5, currentTP, Brushes.LimeGreen, DashStyleHelper.Dash, 2);

                }
                else if (Close[0] < openingLow && bearishFVG)
                {
if (dollarRiskPerContract > maxAllowedRisk)
{
    Print($"[RISK BLOCKED] Risk ${dollarRiskPerContract:F2} > Max Allowed ${maxAllowedRisk:F2}");
    return;
}
                    entryPrice = Close[0];
initialRisk = Math.Abs(entryPrice - Open[2]);
currentTP = entryPrice - 2 * initialRisk;

EnterShort("ORB_Short" + timestamp);
SetStopLoss("ORB_Short" + timestamp, CalculationMode.Price, Open[2], false);
SetProfitTarget("ORB_Short" + timestamp, CalculationMode.Price, currentTP);
					
                    trailingStopPrice = openingClose;
                    tradePlaced = true;
					 entryBar = CurrentBar; // store entry bar


                    Draw.ArrowDown(this, "ShortEntry" + CurrentBar, false, 0, High[0] + 2 * TickSize, Brushes.Red);
					Draw.Line(this, "Entry_Line"+timestamp, false,  0,Close[0], 5, Close[0], Brushes.Blue, DashStyleHelper.Dash, 2);
					Draw.Line(this, "SL_Short_Line"+timestamp, false, 0, Open[2], 5, Open[2], Brushes.Green, DashStyleHelper.Dash, 2);
    				Draw.Line(this, "TP_Short_Line"+timestamp, false, 0, Close[0] - reward, 5, currentTP, Brushes.Orange, DashStyleHelper.Dash, 2);
              }
            }

          if (tradePlaced && Position.MarketPosition != MarketPosition.Flat)
{
    if (Position.MarketPosition == MarketPosition.Long)
    {
        double swingLow = GetSwingLow(swingStrength);
        if (!double.IsNaN(swingLow) && swingLow > trailingStopPrice)
        {
            trailingStopPrice = swingLow;

            ExitLongStopMarket(0, false, 0, trailingStopPrice, "TrailingStop", "ORB_Long" + timestamp);

            Draw.Line(this, "TS_Long_Line" + timestamp, false, 0, trailingStopPrice, 5, trailingStopPrice,
                Brushes.DarkRed, DashStyleHelper.Dash, 2);
        }
    }
    else if (Position.MarketPosition == MarketPosition.Short)
    {
        double swingHigh = GetSwingHigh(swingStrength);
        if (!double.IsNaN(swingHigh) && swingHigh < trailingStopPrice)
        {
            trailingStopPrice = swingHigh;

            ExitShortStopMarket(0, false, 0, trailingStopPrice, "TrailingStop", "ORB_Short" + timestamp);

            Draw.Line(this, "TS_Short_Line" + timestamp, false, 0, trailingStopPrice, 5, trailingStopPrice,
                Brushes.DarkGreen, DashStyleHelper.Dash, 2);
        }
    }
	
	if (Position.MarketPosition == MarketPosition.Long)
{
    if (currentTP - trailingStopPrice < 2 * initialRisk)
    {
        currentTP += initialRisk;
        SetProfitTarget("ORB_Long" + timestamp, CalculationMode.Price, currentTP);

        Draw.Line(this, "ExtendedTP_Long_" + CurrentBar, false, 0, currentTP, 5, currentTP,
            Brushes.Gold, DashStyleHelper.Dash, 2);

        Print($"[TP Extended - Long] New TP = {currentTP}, trailingStop = {trailingStopPrice}");
    }
}else if (Position.MarketPosition == MarketPosition.Short)
{
    if (trailingStopPrice - currentTP < 2 * initialRisk)
    {
        currentTP -= initialRisk;
        SetProfitTarget("ORB_Short" + timestamp, CalculationMode.Price, currentTP);

        Draw.Line(this, "ExtendedTP_Short_" + CurrentBar, false, 0, currentTP, 5, currentTP,
            Brushes.Gold, DashStyleHelper.Dash, 2);

        Print($"[TP Extended - Short] New TP = {currentTP}, trailingStop = {trailingStopPrice}");
    }
}


}



			
			
        }

        protected override void OnExecutionUpdate(Execution execution, string executionId, double price,
            int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            if (execution?.Order == null || execution.Order.Name == null)
                return;

            if (execution.Order.Name.Contains("ORB_"))
            {
                Print($"[LOG] {execution.Order.OrderType} | {execution.Order.Name} | Qty={quantity} | Price={price} | Time={time}");
            }
        }

        private double GetSwingLow(int strength)
        {
            if (CurrentBar < strength * 2 + 1)
                return double.NaN;

            double lowest = Low[0];
            for (int i = 1; i <= strength * 2; i++)
                if (Low[i] < lowest)
                    lowest = Low[i];

            return lowest;
        }

        private double GetSwingHigh(int strength)
        {
            if (CurrentBar < strength * 2 + 1)
                return double.NaN;

            double highest = High[0];
            for (int i = 1; i <= strength * 2; i++)
                if (High[i] > highest)
                    highest = High[i];

            return highest;
        }
    }
}
