get-childitem -Include .vs,bin,obj -Recurse -force | ? { $_.FullName -inotmatch 'node_modules' } | Remove-Item -Force -Recurse