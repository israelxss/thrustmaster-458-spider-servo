# Steering Wheel Closed-Loop Servo Controller 🎮🏎️
### בקרת סרבו חוג-סגור להגה מרוצים (Force Feedback Racing Wheel Servo System)

מערכת מלאה לבקרת מנוע הגה (FFB) בחוג סגור, המאפשרת סיבוב מדויק לכל זווית, שליטה במהירות, נעילה אקטיבית, שחרור מלא (גלגל חופשי), כיול גבולות מכניים וטלמטריה בזמן אמת – **ללא צורך בהרשאות מנהל (Zero UAC)**.

---

## תכונות מרכזיות (Key Features)

* **שליטה בזווית ומהירות (`goto`):** סיבוב הגה מבוקר לכל זווית רצויה (`-450.0°` עד `+450.0°`) במהירות מוגדרת (`°/s`), עם מנגנון מובנה למניעת חריגה (Anti-Overshoot) ושבירת חיכוך סטטי (Adaptive Stall Recovery).
* **בחירת מצב סיום:** אפשרות לבחור האם לאחר ההגעה לזווית ההגה יישאר **נעול** (`lock`) או **משוחרר** (`release`).
* **נעילת הגה בכל נקודה (`lock`):** נעילה רובוטית חזקה מיידית בנקודה הנוכחית של ההגה או בזווית מבוקשת, לזמן מוגדר או ללא הגבלת זמן.
* **שחרור מלא ללא התנגדות (`release`):** שחרור מלא של המנוע וביטול קפיץ המרכוז של היצרן (Free Float / Zero Resistance).
* **איפוס דרייבר מהיר (`reset`):** איפוס ורענון מלא של מנוע הכוח בדרייבר תוך פחות משנייה במקרה הצורך.
* **כיול גבולות אוטומטי (`calibrate`):** מציאת גבולות מכניים שמאלה וימינה (Hardstops) ומרכוז מושלם של ההגה לאמצע האמיתי.
* **מוניטור טלמטריה בזמן אמת (`monitor`):** הצגת זווית ההגה והדוושות בזמן אמת בטרמינל.
* **אפס חלונות מנהל (Zero UAC):** שירות הרקע פועל תחת `SYSTEM`, ומאפשר לכל סקריפט רגיל לשלוט במנוע מיידית ללא צורך בהרשאות מנהל.

---

## פקודות שימוש מהיר (CLI Commands)

כל הפקודות מופעלות דרך [`servo_controller.py`](servo_controller.py):

```powershell
# 1. הגעה לזווית עם שליטה במהירות ושחרור בסיום:
python servo_controller.py goto 200 40 release
python servo_controller.py goto -90 120 release

# 2. הגעה לזווית ונעילה בסיום (נעילה קבועה או למספר שניות):
python servo_controller.py goto 0 100 lock
python servo_controller.py goto 45 60 lock 5

# 3. נעילת ההגה בנקודה הנוכחית (קבוע או למספר שניות):
python servo_controller.py lock
python servo_controller.py lock 10

# 4. שחרור מלא של ההגה (הגה חופשי לגמרי):
python servo_controller.py release

# 5. איפוס מהיר של מנוע הדרייבר:
python servo_controller.py reset

# 6. כיול גבולות מכניים ומרכוז פיזי:
python servo_controller.py calibrate

# 7. קביעת הנקודה הנוכחית כ-0.0° (Homing):
python servo_controller.py zero

# 8. ניטור זווית ההגה בזמן אמת בטרמינל:
python servo_controller.py monitor
```

---

## בדיקות ואימות מערכת (Automated Test Suite)

כדי לאמת את כל מצבי המערכת באופן פיזי על ההגה:

```powershell
python test_all_modes.py
```

הבדיקה מריצה 6 מבחנים אוטומטיים:
1. איפוס דרייבר ובדיקת סטטוס וחיבור מנוע.
2. סיבוב ימינה ושמאלה ובדיקת תגובת כוח.
3. הגעה מדויקת במהירות איטית (35°/s) ושחרור.
4. הגעה מדויקת במהירות מהירה (140°/s) ושחרור.
5. הגעה למרכז (0°) ונעילה אקטיבית (`Holding`).
6. שחרור מיידי למצב צף (`Released`).

---

## ארכיטקטורת המערכת (Architecture)

```
+-------------------------------------------------------------+
|             User Application / Python Scripts               |
|      (servo_controller.py / wheel_motor_api.py)             |
+-------------------------------------------------------------+
                              |
                     HTTP REST (Port 16582)
                              |
+-------------------------------------------------------------+
|    WheelCompatibilityService (Windows Service under SYSTEM) |
|  - HttpMotorServer (Embedded High-Speed HTTP Listener)      |
|  - WheelMotorController (Thread-Safe Motor Engine)          |
|  - Windows.Gaming.Input.ForceFeedback (WinRT Engine)        |
+-------------------------------------------------------------+
                              |
                      USB HID / Xbox GIP
                              |
+-------------------------------------------------------------+
|            Physical Steering Wheel & Motor Hardware         |
|      (Thrustmaster, Logitech, Fanatec, Direct Drive)        |
+-------------------------------------------------------------+
```

---

## התקנה ראשונית (One-Time Setup)

1. **הגדרת הרשאות קבועות ללא UAC:**
   הפעל פעם אחת בלבד את הסקריפט:
   ```powershell
   powershell -ExecutionPolicy Bypass -File setup_permanent_admin.ps1
   ```
2. השירות מותקן ומנוהל בכתובת `http://127.0.0.1:16582`.
3. מעתה ואילך, כל הפקודות והסקריפטים פועלים ממשתמש רגיל ללא שום בקשת מנהל.

---

## הידור מקוד מקור (Compiling from Source - Optional)

> [!NOTE]
> **אין חובה לקמפל!** התיקייה [`published_service/`](published_service/) כבר מכילה את כל קבצי ההרצה הבינאריים המוכנים והמעודכנים (Self-contained Plug & Play).

אם בכל זאת תרצה לקמפל בעצמך מקוד המקור (ללא שום צורך בהתקנת Visual Studio הכבדה, אלא רק ב-CLI בסיסי ומינימלי):

1. **התקנת ה-CLI בלבד (אם טרם מותקן):**
   ```powershell
   winget install Microsoft.DotNet.SDK.8
   ```
2. **הידור בפקודה אחת בודדת:**
   ```powershell
   dotnet publish service_source/WheelCompatibilityService/WheelCompatibilityService.csproj -c Release -o published_service
   ```
3. **עדכון השירות המותקן (ללא צורך במנהל):**
   ```powershell
   Stop-Service WheelCompatibilityService
   Copy-Item published_service\* 'C:\Program Files (x86)\XboxWheelCompatibility\Service\' -Force -Recurse
   Start-Service WheelCompatibilityService
   ```

---

## מבנה הפרויקט (Project Structure)

* [`servo_controller.py`](servo_controller.py) - בקר הסרבו הראשי והממשק למשתמש.
* [`wheel_motor_api.py`](wheel_motor_api.py) - ספריית Python לתקשורת מול ה-API של המנוע.
* [`test_all_modes.py`](test_all_modes.py) - סוויטת בדיקות אוטומטית מלאה.
* [`setup_permanent_admin.ps1`](setup_permanent_admin.ps1) - סקריפט הגדרת הרשאות חד-פעמי.
* [`published_service/`](published_service/) - קבצי ההפעלה המהודרים של השירות (Plug & Play ללא תלות ב-SDK).
* [`service_source/`](service_source/) - קוד המקור ב-C# (.NET) של שירות המנוע ו-`WheelMotorController`.
* [`WHEEL_MOTOR_PROTOCOL.md`](WHEEL_MOTOR_PROTOCOL.md) - תיעוד טכני מלא של הפרוטוקול ונקודות הקצה ב-HTTP.
* [`tmp/`](tmp/) - ספריה המכילה סקריפטים וקבצי בדיקה זמניים שנשמרו בצד.

---

## רישיון (License)
פרויקט זה מופץ תחת רישיון MIT.
