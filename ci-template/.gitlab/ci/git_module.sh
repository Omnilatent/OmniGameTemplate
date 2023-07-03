#!/bin/bash

echo "git LFS init"
rm -rf .git/hooks
git lfs install
git lfs pull
echo "git LFS pull done"
echo "Init git submodules"
git submodule update --init --recursive
echo "git submodules init done"
