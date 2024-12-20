set -e

echo remove existing cred providers
rm -Rf $HOME/.nuget/plugins/netcore/CredentialProvider*
rm -Rf $HOME/.nuget/plugins/netfx/CredentialProvider*

echo install linux side
dotnet build
mkdir --parents $HOME/.nuget/plugins/netcore/CredentialProvider.Redirect
cp -R bin/Debug/net8.0/* $HOME/.nuget/plugins/netcore/CredentialProvider.Redirect/

echo install windows side
dotnet publish -r win-x64 --self-contained
export USERPROFILE=$(echo $(cmd.exe /c echo %USERPROFILE% 2>/dev/null) | sed 's/\\/\//g' | sed 's/\(.\):/\/mnt\/\L\1/g' | sed 's/\r//g')
mkdir --parents "$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.Redirect/"
cp -R bin/Release/net8.0/win-x64/publish/* $USERPROFILE/.nuget/plugins/helpers/CredentialProvider.Redirect/

echo override Linux CredentialProvider.Redirect to go to Windows CredentialProvider.Redirect
echo export NUGET_CREDENTIALPROVIDER_REDIRECT_TARGET=$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.Redirect/CredentialProvider.Redirect.exe >> ~/.bashrc
