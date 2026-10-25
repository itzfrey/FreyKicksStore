namespace FreyKicksStore.Models;

public class HeroSlideItem
{
    public string Category { get; set; } = "Shop";
    public string Eyebrow { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Url { get; set; } = "/shop";
    public bool LoadEagerly { get; set; }
}
