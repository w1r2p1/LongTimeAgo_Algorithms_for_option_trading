   M S B A M L   `   `   `  ����G  COptionsThugs, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null5clr-namespace:OptionsThugs.ViewOptionsThugs.View  5      +    %OptionsThugs.View.StrategyControlView   5      W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      1localclr-namespace:OptionsThugs.View     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      S OStockSharp.Xaml.Diagram, Version=4.3.21.0, Culture=neutral, PublicKeyToken=nullT PStockSharp.Xaml.Charting, Version=4.3.21.0, Culture=neutral, PublicKeyToken=nullK GStockSharp.Xaml, Version=4.3.21.0, Culture=neutral, PublicKeyToken=null/ss"http://schemas.stocksharp.com/xaml    5        � Title$  RobotControl��5	      $	��900��5
      $	��300��5       � 
ResizeMode$ NoResize=�5      .��5      � .+�5   
   ��  �� Orientation$ Vertical=�6   .+�5      �  � ColumnDefinitions 5      �� 5      �� 5      5      �� 5      $
��Left=�6   $ 
Horizontal=�64   .+�5      �� $	��100��5      $��20��5      $��5q�5      $��
Start/Stop��5      �� 5      $	��100��5      $��20��5      $��5q�5       $��Prepare��5!      �� 5#      $	��100��5$      $��20��5%      $��5,0,10,0q�5&      �� 5(      $	��100��5)      $��20��5*      $��5q�5+      5-      �� 5/      $��5,5,0,5q�50      $
��Left=�51      $ 
Horizontal=�52      .+�53      �� $��Connection status:��6   �� 54      $��70��55      $	��5,0q�56       �� 
IsReadOnly . 57      �� 58      $��State:��6   �� 59      $��70��5:      $	��5,0q�5;       . 5<      �� 5=      $��Orders placed:��6   �� 5>      $��65��5?      $	��5,0q�5@       . 5A      �� 5C      $��50��5D      $��20��5E      $	��5,0q�5F      $
��Help��5G      5H      �� 5J      $��0,5,0,5q�5K      $
��Left=�5L      $ 
Horizontal=�5M      .+�5N      �� $��65��5O      $��20��5P      $��5,0,10,0q�5Q      �� 5R      $
��PnL:��6   �� 5S      $��70��5T      $��5,0,5,0q�5U       . 5V      �� 5W      $��Errors#:��6   �� 5X      $��60��5Y      $��5,0,5,0q�5Z       . 5[      �� 5]      $��Deals done:��6   �� 5^      $��70��5_      $��15,0,5,0q�5`       . 5a      �� 5b      $��50��5c      $��20��5d      $	��5,0q�5e      $��Reset��5f      5g      �� 5h      $��0,5,0,5q�5i      $
��Left=�5j      $ 
Horizontal=�5k      .+�5l        StockSharp.Xaml.OrderGrid  $	��435��5m      $	��160��5n      $��5q�5o      !  StockSharp.Xaml.MyTradeGrid  5p      $	��435��5q      $	��160��5r      $��5q�5s      5t      5u      �� 5v   
   -   5         ��Name$ testBox��5w      $	��364��5x      $��
0,5,10,177q�5y      $��Right=�5z      $ �� HorizontalScrollBarVisibility$
 Auto=�5{       �� TextWrapping$
 Wrap=�5|      " �� VerticalScrollBarVisibility$ Visible=�5}      5~      5      