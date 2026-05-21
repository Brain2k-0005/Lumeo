namespace Lumeo;

public class FormContext
{
    public Dictionary<string, List<string>> Errors { get; set; } = new();
    public HashSet<string> DirtyFields { get; set; } = new();
    public bool IsSubmitting { get; set; }
    public bool IsValid => Errors.Count == 0 || Errors.All(e => e.Value.Count == 0);

    // Tracks which fields currently have an in-flight async validation. The field
    // name (or a synthetic key when Name is unset) is added when validation starts
    // and removed when it completes or is cancelled.
    public HashSet<string> ValidatingFields { get; set; } = new();

    /// <summary>
    /// True when at least one FormField has an async validation in flight. Useful
    /// for disabling submit buttons while server-side checks are pending.
    /// </summary>
    public bool IsAnyFieldValidating => ValidatingFields.Count > 0;

    public List<string> GetFieldErrors(string fieldName) =>
        Errors.TryGetValue(fieldName, out var errors) ? errors : new();

    public bool HasError(string fieldName) => GetFieldErrors(fieldName).Count > 0;
    public bool IsDirty(string fieldName) => DirtyFields.Contains(fieldName);
    public bool IsFieldValidating(string fieldName) => ValidatingFields.Contains(fieldName);

    public void MarkDirty(string fieldName) => DirtyFields.Add(fieldName);

    public void MarkValidating(string fieldName, bool validating)
    {
        if (validating) ValidatingFields.Add(fieldName);
        else ValidatingFields.Remove(fieldName);
    }

    public void SetFieldError(string fieldName, string? error)
    {
        if (string.IsNullOrEmpty(error))
            Errors.Remove(fieldName);
        else
            Errors[fieldName] = new List<string> { error };
    }

    public void ClearErrors() => Errors.Clear();
    public void ClearFieldErrors(string fieldName) => Errors.Remove(fieldName);

    public void Reset()
    {
        Errors.Clear();
        DirtyFields.Clear();
        ValidatingFields.Clear();
        IsSubmitting = false;
    }
}
