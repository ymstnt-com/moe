using System.Text.RegularExpressions;
using Discord.WebSocket;
using TNTBot.Models;

namespace TNTBot.Services
{
  public class CustomCommandService
  {
    public CustomCommandService()
    {
      CreateCustomCommandsTable().Wait();
    }

    public async Task<bool> HasCommands(SocketGuild guild)
    {
      var sql = "SELECT COUNT(*) FROM custom_commands WHERE guild_id = $0";
      var count = await DatabaseService.QueryFirst<int>(sql, guild.Id);
      return count > 0;
    }

    public async Task<bool> HasCommand(SocketGuild guild, string name)
    {
      name = CleanCommandName(name);
      var sql = "SELECT COUNT(*) FROM custom_commands WHERE guild_id = $0 AND name = $1";
      var count = await DatabaseService.QueryFirst<int>(sql, guild.Id, name);
      return count > 0;
    }

    public async Task<List<CustomCommand>> GetCommands(SocketGuild guild)
    {
      var sql = "SELECT name, response, description FROM custom_commands WHERE guild_id = $0 ORDER BY name";
      var commands = await DatabaseService.Query<string, string, string>(sql, guild.Id);
      return commands.ConvertAll(x => new CustomCommand(x.Item1!, x.Item2!, x.Item3));
    }

    public async Task<CustomCommand?> GetCommand(SocketGuild guild, string name)
    {
      name = CleanCommandName(name);
      var sql = "SELECT response, description FROM custom_commands WHERE guild_id = $0 AND name = $1";
      var command = await DatabaseService.Query<string, string>(sql, guild.Id, name);
      if (command.Count == 0)
      {
        return null;
      }

      return new CustomCommand(name, command[0].Item1!, command[0].Item2);
    }

    public async Task AddCommand(SocketGuild guild, string name, string response, string? description)
    {
      name = CleanCommandName(name);
      var sql = "INSERT INTO custom_commands(guild_id, name, response, description) VALUES($0, $1, $2, $3)";
      await DatabaseService.NonQuery(sql, guild.Id, name, response, description);
    }

    public async Task RemoveCommand(SocketGuild guild, string name)
    {
      name = CleanCommandName(name);
      var sql = "DELETE FROM custom_commands WHERE guild_id = $0 AND name = $1";
      await DatabaseService.NonQuery(sql, guild.Id, name);
    }

    public List<ParameterErrorInfo> GetParameterErrorInfos(string response)
    {
      return Regex.Matches(response, @"\$(\d+)")
        .Select(x => new ParameterErrorInfo()
        {
          DollarIndex = int.Parse(x.Groups[1].Value),
          ResponseStartIndex = x.Index,
          ResponseEndIndex = x.Index + x.Length
        })
        .DistinctBy(x => x.DollarIndex)
        .OrderBy(x => x.DollarIndex)
        .ToList();
    }

    public string CleanCommandName(string name)
    {
      var prefix = ConfigService.Config.CommandPrefix;
      return Regex.Replace(name, $"^({prefix})+", "");
    }

    public string PrefixCommandName(string name)
    {
      return ConfigService.Config.CommandPrefix + CleanCommandName(name);
    }

    private async Task CreateCustomCommandsTable()
    {
      var sql = @"
        CREATE TABLE IF NOT EXISTS custom_commands(
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          guild_id INTEGER NOT NULL,
          name TEXT NOT NULL,
          response TEXT NOT NULL,
          description TEXT
        )";
      await DatabaseService.NonQuery(sql);
    }
  }
}