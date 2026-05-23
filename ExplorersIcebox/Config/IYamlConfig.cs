namespace ExplorersIcebox.Config;

public interface IYamlConfig
{
    static abstract string ConfigPath { get; }
    void Save();
}
