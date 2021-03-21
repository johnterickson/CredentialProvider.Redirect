set -e

echo remove existing cred providers
rm -Rf $HOME/.nuget/plugins/CredentialProvider*

echo install linux side
dotnet build
mkdir --parents $HOME/.nuget/plugins/netcore/CredentialProvider.WSL2
cp -R bin/Debug/net5.0/* $HOME/.nuget/plugins/netcore/CredentialProvider.WSL2/

echo install windows side
dotnet build -r win10-x64
export USERPROFILE=$(echo $(cmd.exe /c echo %USERPROFILE% 2>/dev/null) | sed 's/\\/\//g' | sed 's/\(.\):/\/mnt\/\L\1/g' | sed 's/\r//g')
mkdir --parents "$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/"
cp -R bin/Debug/net5.0/win10-x64/* $USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/

echo override Linux CredentialProvider.WSL2 to go to Windows CredentialProvider.WSL2
echo export NUGET_WSL_REDIRECT_TO_EXEC=$USERPROFILE/.nuget/plugins/helpers/CredentialProvider.WSL2/CredentialProvider.WSL2.exe >> ~/.bashrc
