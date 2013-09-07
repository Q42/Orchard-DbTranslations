Orchard-DbTranslations
======================

Enables interface translation feature directly in Orchard's admin panels instead of .po files

Folked from Q42/Orchard-DbTranslations...

Changes:
+ Work with Orchard 1.7
+ Import -> Generate From Source will exclude the _Backup folder, which was used by Orchard to store old versions of modules (just ignore if the name exists in the path, so may work with Themes, not verified yet)
+ Import -> Generate from source: added options to generate from a specific module or theme
+ More
