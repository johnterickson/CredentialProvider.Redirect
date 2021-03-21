Tested in Ubuntu 18.04 WSL2

If you didn't know: you can use your Windows enivornment to provide auth to `git` inside of WSL by configuring the `git` to shell out to the Windows credential provider:
```
credential.helper=/mnt/c/Program\ Files/Git/mingw64/libexec/git-core/git-credential-manager.exe
```

The aim of this project is to do the same for `dotnet`'s `nuget` operations by creating a credential provider that runs inside of WSL and re-routes to the Windows credential provider.

Ideally the flow would be:
```
dotnet restore in WSL2 <-> CredentialProvider.WSL2 in WSL2 <-> netfx/netcore CredentialProvider.Microsoft in Windows
```

However, I'm having trouble getting the `stdin`/`stdout` flow working right between Windows and WSL2.   For now, I'm only able to get it to work as:
```
dotnet restore in WSL2 <-> CredentialProvider.WSL2 in WSL2 <-> CredentialProvider.WSL2 in Windows <-> netfx/netcore CredentialProvider.Microsoft in Windows
```

Until that is fixed, you have to do some extra steps:

Windows:
```
echo Make sure you have the latest cred providers
echo iex "& { $(irm https://aka.ms/install-artifacts-credprovider.ps1) } -AddNetfx" | powershell -Command -
```

WSL2:
```
# remove actual cred provider
rm -Rf "$HOME/.nuget/plugins"

# grab sources
git clone https://github.com/johnterickson/CredentialProvider.WSL2.git
cd CredentialProvider.WSL2

# install linux side
dotnet build
mkdir --parents $HOME/.nuget/plugins/netcore/CredentialProvider.WSL2
cp bin/Debug/net5.0/* $HOME/.nuget/plugins/netcore/CredentialProvider.WSL2/

# install windows side
dotnet build -r win10-x64
export USERPROFILE=$(echo $(cmd.exe /c echo %USERPROFILE% 2>/dev/null) | sed 's/\\/\//g' | sed 's/\(.\):/\/mnt\/\L\1/g' | sed 's/\r//g')
mkdir --parents "$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/"
cp bin/Debug/net5.0/win10-x64/publish/* $USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/

# override Linux CredentialProvider.WSL2 to go to Windows CredentialProvider.WSL2
echo export NUGET_WSL_REDIRECT_TO_EXEC=$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/CredentialProvider.WSL2.exe >> ~/.bashrc
```

