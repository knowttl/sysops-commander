#Requires -Version 5.1

Get-LocalGroupMember -Group 'Administrators' |
    Select-Object Name, ObjectClass, PrincipalSource
