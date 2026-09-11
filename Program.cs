using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VortexBot
{
    class Program
    {
        private const ulong OWNER_ID = 1432638241177075827;
        private const string DATA_FILE = "vortex_data.json";
        private const long NEW_USER_BALANCE = 1000000;
        private const int COMMAND_COOLDOWN_SECONDS = 20;
        private const int PORT = 10000;

        private DiscordSocketClient _client;
        private Dictionary<ulong, long> _balances = new();
        private Dictionary<ulong, DateTime> _dailyCooldown = new();
        private Dictionary<ulong, DateTime> _workCooldown = new();
        private Dictionary<ulong, DateTime> _jailedUntil = new();
        private Dictionary<ulong, DateTime> _userCooldown = new();

        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task MainAsync()
        {
            LoadData();
            StartDummyWebServer();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            string token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("❌ DISCORD_TOKEN not set!");
                return;
            }

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;

            try { await _client.LoginAsync(TokenType.Bot, token); await _client.StartAsync(); }
            catch { Console.WriteLine("❌ Login failed!"); return; }

            await Task.Delay(-1);
        }

        private void StartDummyWebServer()
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://+:{PORT}/");
                listener.Start();
                Task.Run(async () =>
                {
                    while (listener.IsListening)
                    {
                        try
                        {
                            var ctx = await listener.GetContextAsync();
                            var res = ctx.Response;
                            byte[] buf = System.Text.Encoding.UTF8.GetBytes("Vortex Bot Running!");
                            res.ContentLength64 = buf.Length;
                            await res.OutputStream.WriteAsync(buf, 0, buf.Length);
                            res.Close();
                        } catch { }
                    }
                });
            } catch { }
        }

        private Task LogAsync(LogMessage msg) { Console.WriteLine(msg); return Task.CompletedTask; }
        private Task ReadyAsync() { Console.WriteLine("✅ VORTEX BOT ONLINE!"); return Task.CompletedTask; }

        private bool IsOwner(ulong userId) => userId == OWNER_ID;
        private bool CheckCooldown(ulong userId, out TimeSpan rem)
        {
            rem = TimeSpan.Zero;
            if (IsOwner(userId)) return true;
            if (_userCooldown.TryGetValue(userId, out var last))
            {
                rem = last.AddSeconds(COMMAND_COOLDOWN_SECONDS) - DateTime.UtcNow;
                if (rem.TotalSeconds > 0) return false;
            }
            _userCooldown[userId] = DateTime.UtcNow;
            return true;
        }

        private void EnsureUser(ulong userId) { if (!_balances.ContainsKey(userId)) _balances[userId] = NEW_USER_BALANCE; }

        private void SaveData()
        {
            try
            {
                var data = new BotData { Balances = _balances, DailyCooldown = _dailyCooldown, WorkCooldown = _workCooldown, JailedUntil = _jailedUntil };
                File.WriteAllText(DATA_FILE, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            } catch { }
        }

        private void LoadData()
        {
            try
            {
                if (!File.Exists(DATA_FILE)) return;
                var d = JsonSerializer.Deserialize<BotData>(File.ReadAllText(DATA_FILE));
                if (d != null) { _balances = d.Balances ?? new(); _dailyCooldown = d.DailyCooldown ?? new(); _workCooldown = d.WorkCooldown ?? new(); _jailedUntil = d.JailedUntil ?? new(); }
            } catch { _balances = new(); }
        }

        private async Task MessageReceivedAsync(SocketMessage msg)
        {
            var u = msg as SocketUserMessage;
            if (u == null || u.Content.Length < 2 || msg.Author.IsBot) return;
            ulong userId = msg.Author.Id;
            EnsureUser(userId);

            if (_jailedUntil.TryGetValue(userId, out var release) && DateTime.UtcNow < release) return;

            string[] args = u.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = args[0].ToLower();

            if (!CheckCooldown(userId, out var cdRem))
            {
                await msg.Channel.SendMessageAsync($"⏳ Maghintay ng **{(int)cdRem.TotalSeconds} segundo**!");
                return;
            }

            if (cmd == "!balance" || cmd == "!bal")
            {
                await msg.Channel.SendMessageAsync($"💰 Balanse: **{_balances[userId]:N0} Coins**");
                return;
            }

            if (cmd == "!daily")
            {
                if (_dailyCooldown.TryGetValue(userId, out var dLast) && (DateTime.UtcNow - dLast).TotalHours < 24)
                {
                    var tRem = TimeSpan.FromHours(24) - (DateTime.UtcNow - dLast);
                    await msg.Channel.SendMessageAsync($"⏳ Bumalik ka pagkalipas ng **{tRem.Hours}h {tRem.Minutes}m**!");
                    return;
                }
                int reward = new Random().Next(500, 1001);
                _balances[userId] += reward;
                _dailyCooldown[userId] = DateTime.UtcNow;
                SaveData();
                await msg.Channel.SendMessageAsync($"🎁 Claimed! +**{reward:N0} Coins!**");
                return;
            }

            if (cmd == "!work")
            {
                if (_workCooldown.TryGetValue(userId, out var wLast) && (DateTime.UtcNow - wLast).TotalMinutes < 5)
                {
                    var wRem = TimeSpan.FromMinutes(5) - (DateTime.UtcNow - wLast);
                    await msg.Channel.SendMessageAsync($"⏳ Maghintay ng **{wRem.Seconds}s**!");
                    return;
                }
                string[] jobs = { "Nag-code", "Nag-maintain ng server", "Nag-ayos ng makina", "Nagbantay ng perimeter", "Nag-calibrate ng sistema" };
                int earn = new Random().Next(100, 401);
                _balances[userId] += earn;
                _workCooldown[userId] = DateTime.UtcNow;
                SaveData();
                await msg.Channel.SendMessageAsync($"🛠️ {jobs[new Random().Next(jobs.Length)]}! +**{earn:N0} Coins!**");
                return;
            }

            if (cmd == "!lb" || cmd == "!leaderboard")
            {
                var top10 = _balances.OrderByDescending(x => x.Value).Take(10).ToList();
                string text = "🏆 **TOP 10 RICHEST**\n";
                int r = 1;
                foreach (var x in top10)
                {
                    string m = r == 1 ? "🥇" : r == 2 ? "🥈" : r == 3 ? "🥉" : "🏅";
                    text += $"{m} <@{x.Key}> — **{x.Value:N0} Coins**\n"; r++;
                }
                await msg.Channel.SendMessageAsync(text);
                return;
            }

            if (cmd == "!slots")
            {
                long bet = args.Length >= 2 && long.TryParse(args[1], out long b) && b > 0 ? b : 0;
                if (bet > _balances[userId]) { await msg.Channel.SendMessageAsync("❌ Kulang ang pera mo!"); return; }

                string[] common = { "🍒", "🍒", "🍒", "🍒", "🍋", "🍋", "🍋", "🍋", "🍇", "🍇", "7️⃣", "7️⃣" };
                string star = "⭐", diamond = "💎";
                string a, symB, c;
                double roll = new Random().NextDouble();

                // ✅ 50% = Panalo / Triple Common
                if (roll < 0.50)
                {
                    int idx = new Random().Next(common.Length);
                    a = common[idx]; symB = common[idx]; c = common[idx];
                }
                // ⭐ 30% = Triple Star
                else if (roll < 0.80) { a = star; symB = star; c = star; }
                // 💎 20% = Triple Diamond
                else { a = diamond; symB = diamond; c = diamond; }

                string resText; long mul = 0;
                if (a == symB && symB == c)
                {
                    if (a == diamond) { mul = 50; resText = "💎💎💎 **MEGA JACKPOT! 50x PANALO!** 💎💎💎"; }
                    else if (a == star) { mul = 5; resText = "⭐⭐⭐ **BIG WIN! 5x PANALO!** ⭐⭐⭐"; }
                    else { mul = 2; resText = $"🎉 **JACKPOT! 2x PANALO!** {a}{a}{a}"; }
                }
                else if (a == symB || symB == c || a == c) { mul = 1; resText = "✨ **PARES! BALIK ANG TAYA!** ✨"; }
                else { mul = 0; resText = "😔 **WALA. SUBOK ULIT!** 😔"; }

                long win = bet * mul;
                _balances[userId] = _balances[userId] - bet + win;
                SaveData();
                await msg.Channel.SendMessageAsync($"```\n🎰 [ {a} ] [ {symB} ] [ {c} ]\n```\n{resText}\n✅ Kita: **{win:N0} Coins** | Balanse: **{_balances[userId]:N0}**");
                return;
            }

            if (IsOwner(userId))
            {
                if (cmd == "!give" && args.Length >= 3 && ulong.TryParse(args[1].Replace("<@!", "").Replace(">", "").Replace("!", ""), out ulong tid) && long.TryParse(args[2], out long amt))
                {
                    EnsureUser(tid); _balances[tid] += amt; SaveData();
                    await msg.Channel.SendMessageAsync($"👑 Ibinigay ang **{amt:N0} Coins** kay <@{tid}>!");
                    return;
                }
                if (cmd == "!jail" && args.Length >= 3 && ulong.TryParse(args[1].Replace("<@!", "").Replace(">", "").Replace("!", ""), out ulong jid) && int.TryParse(args[2], out int min))
                {
                    _jailedUntil[jid] = DateTime.UtcNow.AddMinutes(min); SaveData();
                    await msg.Channel.SendMessageAsync($"⛓️ <@{jid}> nakulong ng **{min} minuto**!");
                    return;
                }
            }

            if (cmd == "!help")
            {
                await msg.Channel.SendMessageAsync(@"
🏠 **VORTEX BOT COMMANDS**
💰 `!bal / !balance` — Tingnan ang balanse
🎁 `!daily` — Kunin ang pang-araw-araw na premyo
🛠️ `!work` — Magtrabaho at kumita
🎰 `!slots [halaga]` — Subukan ang suwerte
🏆 `!lb / !leaderboard` — Tingnan ang mayayaman
⏳ Lahat ng command: 20 segundong paghihintay
🎰 Slots: 50% Panalo | 30% ⭐5x | 20% 💎50x
");
                return;
            }
        }
    }

    public class BotData
    {
        public Dictionary<ulong, long> Balances { get; set; }
        public Dictionary<ulong, DateTime> DailyCooldown { get; set; }
        public Dictionary<ulong, DateTime> WorkCooldown { get; set; }
        public Dictionary<ulong, DateTime> JailedUntil { get; set; }
    }
}
