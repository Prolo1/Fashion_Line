:: add file one to zip
"C:\Program Files\7-Zip\7z" a -y -tzip %1 %2 -mx5 
:: add file two to zip
"C:\Program Files\7-Zip\7z" a -y -tzip %1 %3 -mx5 
::remove any *.config files from zip
"C:\Program Files\7-Zip\7z" d %1 *.config -r

echo done