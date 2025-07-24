# ICT_ORB_FVG_Visuals

A fully automated trading strategy for **NinjaTrader 8** that combines the **ICT Opening Range Breakout (ORB)** with **Fair Value Gap (FVG)** analysis, enhanced by robust risk management, trailing stop logic, and dynamic take profit extension.

---

## 📌 Features

- **Opening Range (OR)** logic based on configurable start/end times (e.g., NY session 23:30–23:35 AEST).
- Detection and visualization of **Fair Value Gaps** (FVG) for trade setups.
- Dynamic **risk-based position sizing** based on account balance and risk percentage.
- **Reward-to-Risk Ratio (RRR)** based profit targets.
- **Trailing Stop** based on recent swing highs/lows.
- **Auto-adjusting Take Profit** when risk:reward threshold is hit.
- Fully **multi-timeframe configurable** (primary and secondary charts).
- Trade logs and on-chart drawings for easier visualization and debugging.

---

## 🧠 Strategy Logic

1. **Capture Opening Range** from secondary timeframe at the configured time window.
2. **Detect FVG** patterns using 3-bar logic:
   - **Bullish FVG**: `High[2] < Low[0] && Close[2] > Open[2]`
   - **Bearish FVG**: `Low[2] > High[0] && Close[2] < Open[2]`
3. **Filter Trades** based on:
   - FVG displacement (point difference).
   - Risk threshold in $ (based on your account and Risk% setting).
4. **Enter Trades**:
   - Bullish FVG + Price > Opening High → Long
   - Bearish FVG + Price < Opening Low → Short
5. **Manage Trades**:
   - Place Stop Loss at opposite FVG edge.
   - Take Profit at RRR * initial risk.
   - Extend TP if trailing stop closes in.

---

## ⚙️ How to Use

### ✅ Prerequisites

- **NinjaTrader 8**
- Add reference to:
  - `NinjaTrader.NinjaScript.Strategies`
  - `NinjaTrader.Gui`
  - `NinjaTrader.NinjaScript.DrawingTools`

### 🛠️ Installation

1. Open **NinjaTrader 8**.
2. Go to **NinjaScript Editor**.
3. Create a new Strategy and replace its code with `ICT_ORB_FVG_Visuals.cs`.
4. Compile the script.
5. Attach it to your chart with appropriate timeframe.

---

## 🔧 Configurable Parameters

| Setting | Description | Default |
|--------|-------------|---------|
| **Primary Time Frame** | Chart timeframe used for price analysis | 1-Min |
| **Secondary Time Frame** | Timeframe used to capture opening range | 5-Min |
| **Opening Range Start / End Time** | NY time (e.g., "23:30" to "23:35") | `"23:30"` to `"23:35"` |
| **Risk % of Account** | Max percentage of account balance to risk per trade | `1%` |
| **Reward:Risk Ratio** | Multiplier to define TP relative to SL | `2.0` |
| **TP Adjust Threshold** | Multiplier to extend TP when price approaches trailing stop | `3.0` |
| **Swing Strength** | Defines trailing stop swing length | `3` |
| **Minimum FVG Displacement** | Minimum gap in points to validate FVG | `2.0` |

---

## 📊 Visual Elements

- **Opening Range Box**: Light Blue rectangle
- **Bullish FVG**: Green rectangle
- **Bearish FVG**: Red rectangle
- **Entry Line**: Blue dashed
- **Stop Loss Line**: Red or Green dashed
- **Take Profit Line**: Lime or Orange dashed
- **Trailing Stop Line**: Dark Red / Dark Green dashed
- **TP Extensions**: Gold dashed line

---

## 🧪 Example Logs

```plaintext
[RISK ] Risk $210.50 > Max Allowed $250.00
[TP Extended - Long] New TP = 14250.00, trailingStop = 14190.00
[LOG] Buy | ORB_Long20250724093000 | Qty=1 | Price=14300.00 | Time=7/24/2025 09:30:00
