using Discord.WebSocket;
using TNTBot.Models;

namespace TNTBot.Services
{
  public class TemplateService
  {
    private readonly SettingsService settingsService;

    public TemplateService(SettingsService settingsService)
    {
      CreateTemplatesTable().Wait();
      this.settingsService = settingsService;
    }

    public bool IsAuthorized(SocketGuildUser user, ModrankLevel requiredLevel, out string? error)
    {
      return settingsService.IsAuthorized(user, requiredLevel, out error);
    }

    public async Task<bool> HasTemplates(SocketGuild guild)
    {
      var sql = "SELECT COUNT(*) FROM templates WHERE guild_id = $0";
      var count = await DatabaseService.QueryFirst<int>(sql, guild.Id);
      return count > 0;
    }

    public async Task<bool> HasTemplate(SocketGuild guild, string name)
    {
      var sql = "SELECT COUNT(*) FROM templates WHERE guild_id = $0 AND name = $1";
      var count = await DatabaseService.QueryFirst<int>(sql, guild.Id, name);
      return count > 0;
    }

    public async Task<List<(string Name, SocketGuildUser Creator)>> ListTemplates(SocketGuild guild)
    {
      var sql = "SELECT name, creator_id FROM templates WHERE guild_id = $0 AND hidden = false";
      var templates = await DatabaseService.Query<string, ulong>(sql, guild.Id);
      return templates.ConvertAll(x => (x.Item1!, guild.GetUser(x.Item2)));
    }

    public async Task<TemplateModel?> GetTemplate(SocketGuild guild, string name)
    {
      var sql = "SELECT id, creator_id, channel_id, mention_id, hidden, title, description, footer, thumbnail_image_url, large_image_url FROM templates WHERE guild_id = $0 AND name = $1";
      var template = await DatabaseService.Query<int, ulong, ulong, ulong, int, string, string, string, string, string>(sql, guild.Id, name);
      if (template == null)
      {
        return null;
      }

      var t = template[0];
      return new TemplateModel()
      {
        Id = t.Item1,
        Guild = guild,
        Creator = guild.GetUser(t.Item2),
        Name = name,
        Channel = guild.GetTextChannel(t.Item3),
        Mention = t.Item4 == default ? null : guild.GetRole(t.Item4),
        Hidden = t.Item5 > 0,
        Title = t.Item6!,
        Description = t.Item7!,
        Footer = t.Item8,
        ThumbnailImageUrl = t.Item9,
        LargeImageUrl = t.Item10,
      };
    }

    public async Task AddTemplate(TemplateModel t)
    {
      await LogService.LogToFileAndConsole(
        $"Adding template {t.Name} with parameters guild: {t.Creator.Guild}, creator: {t.Creator}, channel: {t.Channel}, mention: {t.Mention}, hidden: {t.Hidden}, title: {t.Title}, description: {t.Description}, footer: {t.Footer}, thumbnailImageUrl: {t.ThumbnailImageUrl}, largeImageUrl: {t.LargeImageUrl}",
        t.Creator.Guild);

      var sql = @"
        INSERT INTO templates(guild_id, creator_id, name, channel_id, mention_id, hidden, title, description, footer, thumbnail_image_url, large_image_url)
        VALUES ($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10)";
      await DatabaseService.NonQuery(sql, t.Creator.Guild.Id, t.Creator.Id, t.Name, t.Channel.Id, t.Mention?.Id, t.Hidden, t.Title, t.Description, t.Footer, t.ThumbnailImageUrl, t.LargeImageUrl);
    }

    public async Task RemoveTemplate(SocketGuild guild, string name)
    {
      await LogService.LogToFileAndConsole(
        $"Removing template {name}", guild);

      var sql = "DELETE FROM templates WHERE guild_id = $0 AND name = $1";
      await DatabaseService.NonQuery(sql, guild.Id, name);
    }

    public bool ValidateTemplateParameters(SocketModal modal, TemplateModel t)
    {
      var allValues = t.Title + t.Description + t.Footer;
      var paramsCount = allValues.Count(x => x == '$');
      var maxParams = 5;

      if (paramsCount > maxParams)
      {
        var dump = GetTemplateDump(t);
        var error = $"Too many $ parameters, maximum is {maxParams}\n" +
          $"Here is the stuff you have entered:\n{dump}";

        modal.RespondAsync(error);
        return false;
      }

      return true;
    }

    public string GetTemplateDump(TemplateModel t)
    {
      return
        $" - **Title**: {t.Title}\n" +
        $" - **Description**: {t.Description}\n" +
        $" - **Footer**: {t.Footer ?? "*Not specified*"}\n" +
        $" - **Thumbnail Image URL**: {t.ThumbnailImageUrl ?? "*Not specified*"}\n" +
        $" - **Large Image URL**: {t.LargeImageUrl ?? "*Not specified*"}";
    }

    private async Task CreateTemplatesTable()
    {
      var sql = @"
        CREATE TABLE IF NOT EXISTS templates(
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          guild_id INTEGER NOT NULL,
          creator_id INTEGER NOT NULL,
          name TEXT NOT NULL,
          channel_id INTEGER NOT NULL,
          mention_id INTEGER,
          hidden INTEGER NOT NULL,
          title TEXT NOT NULL,
          description TEXT NOT NULL,
          footer TEXT,
          thumbnail_image_url TEXT,
          large_image_url TEXT
        )";
      await DatabaseService.NonQuery(sql);
    }
  }
}