using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.FileIO;
namespace Nexus.Core;
public sealed partial class NexusStore
{
    public string Move(string id,string folder)
    {
        var item=Get(id);
        if(!Directory.Exists(folder))throw new DirectoryNotFoundException("Escolha uma pasta existente no disco de destino.");
        var target=System.IO.Path.Combine(System.IO.Path.GetFullPath(folder),System.IO.Path.GetFileName(item.Path));
        if(File.Exists(target))throw new IOException("Já existe um arquivo com esse identificador no destino.");
        // Checkpoint committed WAL data before taking an exclusive Windows file lock.
        using(var c=Database(id))
        using(var check=Command(c,"PRAGMA wal_checkpoint(TRUNCATE)"))
        using(var r=check.ExecuteReader())if(r.Read()&&r.GetInt32(0)!=0)throw new IOException("Feche outros programas que estejam usando este banco.");
        bool created=false,published=false;
        try
        {
            using(var source=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.None))
            {
                if(File.Exists(item.Path+"-wal")&&new FileInfo(item.Path+"-wal").Length>0)throw new IOException("Há alterações pendentes no arquivo WAL. Feche outros programas antes de mover.");
                using(var destination=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {created=true;source.CopyTo(destination);destination.Flush(true);}
                using(var copy=Open(target))
                using(var integrity=Command(copy,"PRAGMA integrity_check"))
                    if(!string.Equals(integrity.ExecuteScalar()?.ToString(),"ok",StringComparison.Ordinal))throw new IOException("A cópia não passou na verificação de integridade.");
                using var meta=Open(catalog);using var cmd=Command(meta,"UPDATE databases SET path=$p0 WHERE id=$p1 AND owner=$p2",target,id,user);
                if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("O catálogo mudou. Nenhum arquivo de origem foi removido.");
                published=true;
            }
            try{File.Delete(item.Path);File.Delete(item.Path+"-wal");File.Delete(item.Path+"-shm");}
            catch(IOException){return "Banco movido e verificado. A cópia antiga permaneceu em: "+item.Path;}
            catch(UnauthorizedAccessException){return "Banco movido e verificado. A cópia antiga permaneceu em: "+item.Path;}
            return "Banco movido e verificado com sucesso.";
        }
        catch
        {
            if(created&&!published)File.Delete(target);
            throw;
        }
    }
    public void Delete(string id)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("A Lixeira é suportada nesta versão para Windows.");
        var item=Get(id);
        // Opening/closing checkpoints WAL and confirms the selected database exists.
        using(var c=Database(id))
        using(var cmd=Command(c,"PRAGMA wal_checkpoint(TRUNCATE)"))
        using(var r=cmd.ExecuteReader())if(r.Read()&&r.GetInt32(0)!=0)throw new IOException("Feche outros programas que estejam usando este banco.");
        using(var locked=new FileStream(item.Path,FileMode.Open,FileAccess.Read,FileShare.None)){}
        FileSystem.DeleteFile(item.Path,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin,UICancelOption.ThrowException);
        using var meta=Open(catalog);using var delete=Command(meta,"DELETE FROM databases WHERE id=$p0 AND owner=$p1",id,user);delete.ExecuteNonQuery();
    }
}
