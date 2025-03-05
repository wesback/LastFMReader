public interface ICacheService
{
    void Set(string key, object value);
    object Get(string key);
    T Get<T>(string key); // Ensure no constraints are applied here unless necessary
    bool Contains(string key);
}
