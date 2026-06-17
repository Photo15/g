using System.Collections.Concurrent;
using System.Text.Json;
using TaxiConnectSA.Models;

namespace TaxiConnectSA.Services
{
    public class UserService
    {
        private readonly string _storePath;
        private readonly ConcurrentDictionary<string, UserModel> _users;

        public UserService(IWebHostEnvironment env)
        {
            _storePath = Path.Combine(env.ContentRootPath, "Data", "users.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
            _users = new ConcurrentDictionary<string, UserModel>(StringComparer.OrdinalIgnoreCase);
            LoadUsers();
        }

        public bool UserExists(string username) => _users.ContainsKey(username);

        public bool ValidateCredentials(string username, string password)
        {
            if (!_users.TryGetValue(username, out var user))
            {
                return false;
            }

            return user.Password == password;
        }

        public bool AddUser(UserModel user)
        {
            if (UserExists(user.Username))
            {
                return false;
            }

            if (!_users.TryAdd(user.Username, user))
            {
                return false;
            }

            SaveUsers();
            return true;
        }

        private void LoadUsers()
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(_storePath);
                var users = JsonSerializer.Deserialize<List<UserModel>>(json);
                if (users is not null)
                {
                    foreach (var user in users)
                    {
                        _users[user.Username] = user;
                    }
                }
            }
            catch
            {
                // Ignore corrupted user store so the app can still run.
            }
        }

        private void SaveUsers()
        {
            var users = _users.Values.ToList();
            var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storePath, json);
        }
    }
}
