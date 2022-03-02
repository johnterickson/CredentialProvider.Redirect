Tested in Ubuntu 18.04 WSL2

If you didn't know: you can use your Windows enivornment to provide auth to `git` inside of WSL by configuring the `git` to shell out to the Windows credential provider:
```
credential.helper=/mnt/c/Program\ Files/Git/mingw64/libexec/git-core/git-credential-manager.exe
```

The aim of this project is to do the same for `dotnet`'s `nuget` operations by creating a credential provider that runs inside of WSL and re-routes to the Windows credential provider.

Ideally the flow would be:
```
dotnet restore in WSL2 <-> CredentialProvider.Redirect in WSL2 <-> netfx/netcore CredentialProvider.Microsoft in Windows
```

However, I'm having trouble getting the `stdin`/`stdout` flow working right between Windows and WSL2.   For now, I'm only able to get it to work as:
```
dotnet restore in WSL2 <-> CredentialProvider.Redirect in WSL2 <-> CredentialProvider.Redirect in Windows <-> netfx/netcore CredentialProvider.Microsoft in Windows
```

Until that is fixed, you have to do some extra steps:

Windows:
```
echo Make sure you have the latest cred providers
iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -AddNetfx" | powershell -Command -
```

WSL2:
```
echo Note this will delete any existing credential providers
chmod +x ./install.sh
./install.sh
```

