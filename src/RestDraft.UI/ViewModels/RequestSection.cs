namespace RestDraft.UI.ViewModels;

// The pages of the request editor, in the order of its tabs. The body comes first and is the page a tab opens on.
public enum RequestSection
{
    Body = 0,
    Params = 1,
    Headers = 2,
    Authorization = 3,
}
