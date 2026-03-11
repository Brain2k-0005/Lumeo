namespace Lumeo;

public class FormContext
{
    public Dictionary<string, List<string>> Errors { get; set; } = new();
    public HashSet<string> DirtyFields { get; set; } = new();
    public bool IsSubmitting { get; set; }
    public bool IsValid => Errors.Count == 0 || Errors.All(e => e.Value.Count == 0);

    public List<string> GetFieldErrors(string fieldName) =>
        Errors.TryGetValue(fieldName, out var errors) ? errors : new();

    public bool HasError(string fieldName) => GetFieldErrors(fieldName).Count > 0;
    public bool IsDirty(string fieldName) => DirtyFields.Contains(fieldName);

    public void MarkDirty(string fieldName) => DirtyFields.Add(fieldName);

    public void ClearErrors() => Errors.Clear();
    public void ClearFieldErrors(string fieldName) => Errors.Remove(fieldName);

    public void Reset()
    {
        Errors.Clear();
        DirtyFields.Clear();
        IsSubmitting = false;
    }
}
