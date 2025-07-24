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
		private string activeOrderTag = string.Empty;
		
		

        #region Variables
        private double riskPercent = 1; // default to 1%
        #endregion
public enum TimeFrameOption
{
    OneMinute = 1,
    FiveMinutes = 5,
    FifteenMinutes = 15,
    ThirtyMinutes = 30,
    OneHour = 60,
    FourHour = 240,
    Daily = 1440
}

[NinjaScriptProperty]
[Display(Name = "Primary Time Frame", GroupName = "Time Frames", Order = 0)]
public TimeFrameOption PrimaryTimeFrame { get; set; } = TimeFrameOption.OneMinute;

[NinjaScriptProperty]
[Display(Name = "Secondary Time Frame", GroupName = "Time Frames", Order = 1)]
public TimeFrameOption SecondaryTimeFrame { get; set; } = TimeFrameOption.FiveMinutes;

        [NinjaScriptProperty]
        [Range(0.1, 100)]
        [Display(Name = "Risk % of Account", Description = "Risk percentage of account per trade", GroupName = "Risk Settings", Order = 1)]
        public double RiskPercent
        {
            get { return riskPercent; }
            set { riskPercent = value; }
        }
		[NinjaScriptProperty]
[Display(Name = "Opening Range Start (HH:mm)", GroupName = "Opening Range", Order = 2)]
public string OpeningRangeStartTime { get; set; } = "23:30";

[NinjaScriptProperty]
[Display(Name = "Opening Range End (HH:mm)", GroupName = "Opening Range", Order = 3)]
public string OpeningRangeEndTime { get; set; } = "23:35";

[NinjaScriptProperty]
[Range(1, 10)]
[Display(Name = "Swing Strength", GroupName = "Trade Settings", Order = 4)]
public int SwingStrength { get; set; } = 3;

[NinjaScriptProperty]
[Range(1.0, 10.0)]
[Display(Name = "Reward:Risk Ratio", GroupName = "Trade Settings", Order = 5)]
public double RewardRiskRatio { get; set; } = 2.0;

[NinjaScriptProperty]
[Range(1.0, 10.0)]
[Display(Name = "TP Adjust Threshold (x Risk)", GroupName = "Trade Settings", Order = 6)]
public double TPAdjustThreshold { get; set; } = 3.0;


[NinjaScriptProperty]
[Range(0.0, double.MaxValue)]
[Display(Name = "Minimum FVG Displacement (points)", GroupName = "FVG Settings", Order = 20)]
public double MinFVGDisplacement { get; set; } = 2.0;  // example: 2 points

private TimeSpan ORStartLocal => TimeSpan.Parse(OpeningRangeStartTime);
private TimeSpan OREndLocal => TimeSpan.Parse(OpeningRangeEndTime);


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

        BarsPeriod = new BarsPeriod
        {
            BarsPeriodType = BarsPeriodType.Minute,
            Value = (int)PrimaryTimeFrame
        };
    }
    else if (State == State.Configure)
    {
        AddDataSeries(BarsPeriodType.Minute, (int)SecondaryTimeFrame);
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
            if (CurrentBars[0] < 15 || CurrentBars[1] < 1)
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

            if (BarsInProgress == 1 && !openingCaptured)
                CaptureOpeningRange();

            if (BarsInProgress == 0 && openingCaptured && !tradePlaced)
            {
                if (currentLocalTime < OREndLocal)
                    return;

                bool bullishFVG = High[2] < Low[0] && Close[2] > Open[2];
                bool bearishFVG = Low[2] > High[0] && Close[2] < Open[2];

                DrawFVGZones(bullishFVG, bearishFVG);
				
				// Apply displacement filters
if (bullishFVG)
{
    double displacement = Low[0] - High[2];
    bullishFVG = displacement >= MinFVGDisplacement;
}

if (bearishFVG)
{
    double displacement = Low[2] - High[0];
    bearishFVG = displacement >= MinFVGDisplacement;
}

                double stopDistancePoints = Math.Abs(Close[0] - Open[3]);
                if (stopDistancePoints < TickSize)
                    return;

                double dollarRisk = CalculateDollarRisk(stopDistancePoints);
                double maxRisk = GetMaxAllowedRisk();
				
				 Print($"[RISK ] Risk ${dollarRisk:F2} > Max Allowed ${maxRisk:F2}");

                if (dollarRisk > maxRisk)
                {
                    Print($"[RISK BLOCKED] Risk ${dollarRisk:F2} > Max Allowed ${maxRisk:F2}");
                    return;
                }

                double reward = RewardRiskRatio * stopDistancePoints;

                if (Close[0] > openingHigh && bullishFVG)
                    PlaceLongTrade(timestamp, reward);
                else if (Close[0] < openingLow && bearishFVG)
                    PlaceShortTrade(timestamp, reward);
            }

            if (tradePlaced && Position.MarketPosition != MarketPosition.Flat)
            {
                ManageTrailingStop(timestamp);
                ExtendProfitTargetIfNeeded(timestamp);
            }
        }

        private void CaptureOpeningRange()
        {
            DateTime barTime5Min = Times[1][0];
            TimeSpan barLocalTime5Min = barTime5Min.TimeOfDay;
			

            if (barLocalTime5Min == OREndLocal)
            {
                openingHigh = Highs[1][0];
                openingLow = Lows[1][0];
                openingClose = Closes[1][0];
                openingCaptured = true;
				
				Print($"barTime5Min {barTime5Min} Highs[1][0] {Highs[1][0]} Lows[1][0] {Lows[1][0]} Closes[1][0] {Closes[1][0]} Opens[1][0] {Opens[1][0]}");

                string tag = "ORBox" + sessionDate.ToString("yyyyMMdd");
                Draw.Rectangle(this, tag, false, 15, openingHigh, 0, openingLow, Brushes.LightBlue, Brushes.Blue, 20);
            }
        }

        private void DrawFVGZones(bool bullishFVG, bool bearishFVG)
        {
            string time = Time[0].ToString("yyyyMMddHHmmss");

            if (bullishFVG)
            {
                Draw.Rectangle(this, "BullFVG_" + time, false,
                    2, Low[0], 0, High[2],
                    Brushes.LightGreen, Brushes.Green, 20);
            }

            if (bearishFVG)
            {
                Draw.Rectangle(this, "BearFVG_" + time, false,
                    2, High[0], 0, Low[2],
                    Brushes.Pink, Brushes.Red, 20);
            }
        }

        private double CalculateDollarRisk(double stopDistancePoints)
        {
            double tickValue = Instrument.MasterInstrument.TickSize * Instrument.MasterInstrument.PointValue;
            double stopDistanceTicks = stopDistancePoints / TickSize;
            return stopDistanceTicks * tickValue;
        }

        private double GetMaxAllowedRisk()
        {
            double accountBalance = Account.Get(AccountItem.CashValue, Currency.UsDollar);
            return RiskPercent * accountBalance/100;
        }

        private void PlaceLongTrade(string timestamp, double reward)
        {
            entryPrice = Close[0];
            initialRisk = Math.Abs(entryPrice - Open[2]);
            currentTP = entryPrice + reward * initialRisk;
			
			activeOrderTag = "ORB_Long" + timestamp;

            EnterLong(activeOrderTag);
            SetStopLoss(activeOrderTag, CalculationMode.Price, Open[2], false);
            SetProfitTarget(activeOrderTag, CalculationMode.Price, currentTP);

            trailingStopPrice = openingClose;
            tradePlaced = true;
            entryBar = CurrentBar;

            Draw.ArrowUp(this, "LongEntry" + CurrentBar, false, 0, Low[0] - 2 * TickSize, Brushes.Green);
            Draw.Line(this, "Entry_Line" + timestamp, false, 0, Close[0], 5, Close[0], Brushes.Blue, DashStyleHelper.Dash, 2);
            Draw.Line(this, "SL_Long_Line" + timestamp, false, 0, Open[2], 5, Open[2], Brushes.Red, DashStyleHelper.Dash, 2);
            Draw.Line(this, "TP_Long_Line" + timestamp, false, 0, currentTP, 5, currentTP, Brushes.LimeGreen, DashStyleHelper.Dash, 2);
        }

        private void PlaceShortTrade(string timestamp, double reward)
        {
            entryPrice = Close[0];
            initialRisk = Math.Abs(entryPrice - Open[2]);
            currentTP = entryPrice - reward * initialRisk;
			activeOrderTag = "ORB_Short" + timestamp;

            EnterShort(activeOrderTag);
            SetStopLoss(activeOrderTag, CalculationMode.Price, Open[2], false);
            SetProfitTarget(activeOrderTag, CalculationMode.Price, currentTP);

            trailingStopPrice = openingClose;
            tradePlaced = true;
            entryBar = CurrentBar;

            Draw.ArrowDown(this, "ShortEntry" + CurrentBar, false, 0, High[0] + 2 * TickSize, Brushes.Red);
            Draw.Line(this, "Entry_Line" + timestamp, false, 0, Close[0], 5, Close[0], Brushes.Blue, DashStyleHelper.Dash, 2);
            Draw.Line(this, "SL_Short_Line" + timestamp, false, 0, Open[2], 5, Open[2], Brushes.Green, DashStyleHelper.Dash, 2);
            Draw.Line(this, "TP_Short_Line" + timestamp, false, 0, currentTP, 5, currentTP, Brushes.Orange, DashStyleHelper.Dash, 2);
        }

        private void ManageTrailingStop(string timestamp)
        {
            if (Position.MarketPosition == MarketPosition.Long)
            {
                double swingLow = GetSwingLow(SwingStrength);
                if (!double.IsNaN(swingLow) && swingLow > trailingStopPrice)
                {
                    trailingStopPrice = swingLow;
                    
					SetStopLoss(activeOrderTag, CalculationMode.Price, trailingStopPrice, false);
                    Draw.Line(this, "TS_Long_Line" + timestamp, false, 0, trailingStopPrice, 5, trailingStopPrice, Brushes.DarkRed, DashStyleHelper.Dash, 2);
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                double swingHigh = GetSwingHigh(SwingStrength);
                if (!double.IsNaN(swingHigh) && swingHigh < trailingStopPrice)
                {
                    trailingStopPrice = swingHigh;
                   
					SetStopLoss(activeOrderTag, CalculationMode.Price, trailingStopPrice, false);
                    Draw.Line(this, "TS_Short_Line" + timestamp, false, 0, trailingStopPrice, 5, trailingStopPrice, Brushes.DarkGreen, DashStyleHelper.Dash, 2);
                }
            }
        }

        private void ExtendProfitTargetIfNeeded(string timestamp)
        {
            if (Position.MarketPosition == MarketPosition.Long)
            {
                if (currentTP - trailingStopPrice < TPAdjustThreshold  * initialRisk)
                {
                    currentTP += initialRisk;
                    SetProfitTarget(activeOrderTag, CalculationMode.Price, currentTP);
                    Draw.Line(this, "ExtendedTP_Long_" + CurrentBar, false, 0, currentTP, 5, currentTP, Brushes.Gold, DashStyleHelper.Dash, 2);
                    Print($"[TP Extended - Long] New TP = {currentTP}, trailingStop = {trailingStopPrice}");
                }
            }
            else if (Position.MarketPosition == MarketPosition.Short)
            {
                if (trailingStopPrice - currentTP < TPAdjustThreshold  * initialRisk)
                {
                    currentTP -= initialRisk;
                    SetProfitTarget(activeOrderTag, CalculationMode.Price, currentTP);
                    Draw.Line(this, "ExtendedTP_Short_" + CurrentBar, false, 0, currentTP, 5, currentTP, Brushes.Gold, DashStyleHelper.Dash, 2);
                    Print($"[TP Extended - Short] New TP = {currentTP}, trailingStop = {trailingStopPrice}");
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
