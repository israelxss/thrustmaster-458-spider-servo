using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XboxWheelCompatibility.WheelTransformer;

namespace XboxWheelCompatibility.WheelCompatibilityService
{
    public class HttpMotorServer
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly int _port;

        public HttpMotorServer(int port = 16582)
        {
            _port = port;
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            Task.Run(() => ListenLoop(_cts.Token));
            Console.WriteLine($"[HttpMotorServer] Listening via TcpListener on port {_port}...");
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync(token);
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception)
                {
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                try
                {
                    string? requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(requestLine)) return;

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpperInvariant();
                    string fullUrl = parts[1];

                    // Consume remaining headers
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }

                    string path = fullUrl;
                    string queryString = "";
                    int qIdx = fullUrl.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        path = fullUrl.Substring(0, qIdx);
                        queryString = fullUrl.Substring(qIdx + 1);
                    }
                    path = path.ToLowerInvariant();

                    var query = System.Web.HttpUtility.ParseQueryString(queryString);
                    object responseData;

                    switch (path)
                    {
                        case "/api/status":
                            responseData = WheelMotorController.GetStatus();
                            break;

                        case "/api/motor/reset":
                            bool wasReset = await WheelMotorController.ResetMotorAsync();
                            responseData = new { success = wasReset, action = "reset", status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/release":
                            bool released = await WheelMotorController.ReleaseWheelAsync();
                            responseData = new { success = released, action = "release", status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/hold":
                        case "/api/motor/lock":
                            double? targetAngle = null;
                            if (double.TryParse(query["target"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ta))
                                targetAngle = ta;
                            else if (double.TryParse(query["angle"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double a))
                                targetAngle = a;

                            double holdStrength = 0.8;
                            if (double.TryParse(query["strength"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double s)) holdStrength = s;
                            bool held = await WheelMotorController.HoldWheelAsync(targetAngle, holdStrength);
                            responseData = new { success = held, action = "hold", targetAngle, strength = holdStrength, status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/center":
                            double centerStrength = 0.8;
                            if (double.TryParse(query["strength"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cs)) centerStrength = cs;
                            bool centered = await WheelMotorController.CenterWheelAsync(centerStrength);
                            responseData = new { success = centered, action = "center", strength = centerStrength, status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/rotate":
                            float torque = 0.5f;
                            int durationMs = 500;
                            if (float.TryParse(query["torque"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t)) torque = t;
                            if (int.TryParse(query["duration"], out int d)) durationMs = d;
                            bool rotated = await WheelMotorController.RotateWheelAsync(torque, durationMs);
                            responseData = new { success = rotated, action = "rotate", torque, durationMs, status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/goto":
                            double gotoAngle = 0.0;
                            if (double.TryParse(query["angle"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ga)) gotoAngle = ga;
                            else if (double.TryParse(query["target"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double gt)) gotoAngle = gt;

                            double gotoSpeed = 100.0;
                            if (double.TryParse(query["speed"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double gs)) gotoSpeed = gs;

                            bool lockAtEnd = false;
                            if (!string.IsNullOrEmpty(query["lock"]))
                            {
                                string lk = query["lock"].ToLowerInvariant();
                                lockAtEnd = (lk == "true" || lk == "1" || lk == "yes" || lk == "lock" || lk == "hold");
                            }

                            int gotoTimeout = 8000;
                            if (int.TryParse(query["timeout"], out int gto)) gotoTimeout = gto;

                            bool reached = await WheelMotorController.GotoAngleAsync(gotoAngle, gotoSpeed, lockAtEnd, gotoTimeout);
                            responseData = new { success = reached, action = "goto", targetAngle = gotoAngle, speed = gotoSpeed, lockAtEnd, status = WheelMotorController.GetStatus() };
                            break;

                        case "/api/motor/gain":
                            double gain = 1.0;
                            if (double.TryParse(query["value"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double g)) gain = g;
                            bool gainSet = await WheelMotorController.SetGainAsync(gain);
                            responseData = new { success = gainSet, action = "set_gain", gain, status = WheelMotorController.GetStatus() };
                            break;

                        default:
                            responseData = new { error = "Not found", path };
                            break;
                    }

                    string json = JsonSerializer.Serialize(responseData, new JsonSerializerOptions { WriteIndented = true });
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(json);

                    string headers = $"HTTP/1.1 200 OK\r\n" +
                                     $"Content-Type: application/json; charset=utf-8\r\n" +
                                     $"Content-Length: {bodyBytes.Length}\r\n" +
                                     $"Access-Control-Allow-Origin: *\r\n" +
                                     $"Connection: close\r\n\r\n";

                    byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                    await stream.FlushAsync();
                }
                catch { }
            }
        }
    }
}
