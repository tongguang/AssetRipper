using AssetRipper.Export.Configuration;

namespace AssetRipper.GUI.Web.Pages.Settings.DropDown;

public sealed class SpriteExportModeDropDownSetting : DropDownSetting<SpriteExportMode>
{
	public static SpriteExportModeDropDownSetting Instance { get; } = new();

	public override string Title => Localization.SpriteExportTitle;

	protected override string GetDisplayName(SpriteExportMode value) => value switch
	{
		SpriteExportMode.Yaml => Localization.SpriteFormatYaml,
		SpriteExportMode.Native => Localization.SpriteFormatNative,
		SpriteExportMode.Texture2D => Localization.SpriteFormatTexture,
		SpriteExportMode.SourceImage => Localization.SpriteFormatSourceImage,
		_ => base.GetDisplayName(value),
	};

	protected override string? GetDescription(SpriteExportMode value) => value switch
	{
		SpriteExportMode.Yaml => Localization.SpriteFormatYamlDescription,
		SpriteExportMode.Native => Localization.SpriteFormatNativeDescription,
		SpriteExportMode.Texture2D => Localization.SpriteFormatTextureDescription,
		SpriteExportMode.SourceImage => Localization.SpriteFormatSourceImageDescription,
		_ => base.GetDescription(value),
	};
}
