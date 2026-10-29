using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using RestDraft.UI.Behaviors;
using RestDraft.UI.Services;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;

namespace RestDraft.UI.Controls;

// A read-write or read-only text editor with syntax highlighting for JSON and XML.
// Plain text has no grammar, so the highlighting installation is removed.
public sealed class CodeEditor : TextEditor
{
    public static readonly StyledProperty<EditorLanguage> LanguageProperty =
        AvaloniaProperty.Register<CodeEditor, EditorLanguage>(nameof(Language));

    // TextEditor.Text is not a styled property, so bindings go through EditorText and are copied both ways.
    public static readonly StyledProperty<string> EditorTextProperty =
        AvaloniaProperty.Register<CodeEditor, string>(
            nameof(EditorText),
            defaultValue: string.Empty,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private TextMateInstallation? _installation;

    static CodeEditor()
    {
        LanguageProperty.Changed.AddClassHandler<CodeEditor>((editor, _) => editor.ApplyLanguage());
        EditorTextProperty.Changed.AddClassHandler<CodeEditor>((editor, change) => editor.PushEditorText(change.GetNewValue<string>()));
    }

    public CodeEditor()
    {
        TextChanged += OnTextChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        AddHandler(KeyDownEvent, OnEscapeKeyDown, RoutingStrategies.Tunnel);
    }

    // Themes are looked up by exact type. Without this override, the TextEditor theme (and its template) is not applied.
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public EditorLanguage Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public string EditorText
    {
        get => GetValue(EditorTextProperty);
        set => SetValue(EditorTextProperty, value);
    }

    // Tab types a tab character in an editor, so it cannot also leave the editor. Escape leaves it instead
    // (Shift+Escape goes back), which keeps the body editor from being a keyboard trap.
    private void OnEscapeKeyDown(object? sender, KeyEventArgs e)
    {
        var modifiers = e.KeyModifiers;
        if (e.Key != Key.Escape || (modifiers != KeyModifiers.None && modifiers != KeyModifiers.Shift))
        {
            return;
        }

        var target = FocusNeighbor.Find(this, forward: modifiers != KeyModifiers.Shift);
        e.Handled = target is not null;
        target?.Focus();
    }

    // The highlighting colors belong to the theme, so a switch between light and dark installs the matching set.
    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyLanguage();
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        SetCurrentValue(EditorTextProperty, Text ?? string.Empty);
    }

    private void PushEditorText(string text)
    {
        if (Text != text)
        {
            Text = text;
        }
    }

    private void ApplyLanguage()
    {
        _installation?.Dispose();
        _installation = null;
        if (Language == EditorLanguage.PlainText)
        {
            return;
        }

        var registry = new RegistryOptions(ThemeFor(ActualThemeVariant));
        var installation = this.InstallTextMate(registry);
        installation.SetGrammar(registry.GetScopeByLanguageId(LanguageId(Language)));
        _installation = installation;
    }

    private static ThemeName ThemeFor(Avalonia.Styling.ThemeVariant variant)
    {
        return variant == Avalonia.Styling.ThemeVariant.Dark ? ThemeName.DarkPlus : ThemeName.LightPlus;
    }

    private static string LanguageId(EditorLanguage language)
    {
        return language == EditorLanguage.Json ? "json" : "xml";
    }
}
