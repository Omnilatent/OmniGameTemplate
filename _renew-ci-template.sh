#!/bin/zsh
cd "$(dirname "$0")"
echo "include: \"/ci-template/.gitlab/.gitlab-ci.yml\"" > .gitlab-ci.yml
rm -rf ci-template

git clone https://gitlab.volio.vn/jacatgames/Git-CI-Files.git ci-template

rm -rf ci-template/.git

cp -Rf ci-template/.gitlab/Assets/* ./Assets

#git add -A

#git commit -m "Renew ci-template"

echo "Renew Git CI Template completed."
