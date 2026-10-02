# Windows Hardware Drivers for Xbox Steering Wheel (FFB)
### דרייברי החומרה של Windows עבור הגה מרוצים (Xbox GIP)

תיקייה זו מרכזת את כל דרייברי החומרה המקוריים (`.inf`, `.sys`) של Windows המשמשים לתקשורת, זיהוי ושליטה במנוע של הגה המרוצים (`VID_044F&PID_B664`).

---

## פירוט הדרייברים (Driver Packages)

### 1. `dc1-controller/` (`dc1-controller.inf`, `dc1-controller.sys`)
* **תפקיד:** דרייבר ה-USB הראשי (Xbox Composite Device) המזהה את החומרה ברמת ה-USB Bus.
* **מזהי חומרה נתמכים:**
  * `USB\VID_044F&PID_B664` (הגה Thrustmaster)
  * `USB\MS_COMP_XGIP10` (פרוטוקול Xbox Game Input)

### 2. `xboxgip/` (`xboxgip.inf`, `xboxgip.sys`, `devauthe.sys`)
* **תפקיד:** דרייבר ליבה מרכזי של מיקרוסופט (Kernel-Mode Driver) עבור פרוטוקול ה-GIP (Game Input Protocol) וניהול האימות והתקשורת הדו-כיוונית מול מנוע ה-Force Feedback.

### 3. `xboxgipsynthetic/` (`xboxgipsynthetic.inf`)
* **תפקיד:** דרייבר עבור התקנים סינתטיים וממשק ה-XInput / WinRT של בקרי Xbox.

---

## כיצד להתקין או לרענן ידנית (במידת הצורך)

במידה וההגה לא מזוהה במחשב אחר או שהדרייבר השתבש, ניתן להתקין מחדש דרך PowerShell (כמנהל):

```powershell
pnputil /add-driver dc1-controller\dc1-controller.inf /install
pnputil /add-driver xboxgip\xboxgip.inf /install
```
