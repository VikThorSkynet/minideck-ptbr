using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MiniDeck;
public sealed class MacroEditorWindow : Window
{
    private readonly DeckConfig config;
    private readonly ListBox list=new(){MinWidth=220,Margin=new Thickness(0,0,18,0),Background=Ui.Brush("#101A29"),Foreground=Ui.Brush("#EAF0F7")};
    private readonly ActionEditor editor=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,0),Foreground=Ui.Brush("#FFCC94")};
    private int current=-1;private bool loading;
    public List<DeckAction> Result {get;private set;}
    public MacroEditorWindow(DeckConfig cfg,IEnumerable<DeckAction> steps)
    {
        config=cfg;Result=steps.Select(s=>s.Copy()).ToList();if(Result.Count==0)Result.Add(new(){Name="Esperar",Kind="delay",DelayMs=500});
        Title="MiniDeck · Montar sequência";Width=820;Height=720;MinWidth=680;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Ui.Brush("#151F2E");Foreground=Ui.Brush("#EAF0F7");
        var root=new DockPanel{Margin=new Thickness(22)};Content=root;
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var actions=new WrapPanel();bottom.Children.Add(actions);bottom.Children.Add(status);
        Button Add(string name,Action click){var b=new Button{Content=name};b.Click+=(_,_)=>{try{click();}catch(Exception e){status.Text=e.Message;}};actions.Children.Add(b);return b;}
        Add("+ Adicionar passo",()=>{Commit();if(Result.Count>=100)throw new InvalidOperationException("Máximo de 100 passos.");Result.Add(new(){Name="Novo passo"});Refresh(Result.Count-1);});
        Add("↑ Subir",()=>Move(-1));Add("↓ Descer",()=>Move(1));
        Add("Remover passo",()=>{if(current<0)return;Result.RemoveAt(current);current=-1;Refresh(Math.Min(list.SelectedIndex,Result.Count-1));});
        Add("Salvar passo",()=>{Commit();Refresh(current);status.Text="Passo salvo.";});
        Add("Concluir sequência",()=>{Commit();ActionRules.Validate(new(){Kind="sequence",Steps=Result});DialogResult=true;});
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(240)});grid.ColumnDefinitions.Add(new());root.Children.Add(grid);grid.Children.Add(list);
        var scroll=new ScrollViewer{Content=editor,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetColumn(scroll,1);grid.Children.Add(scroll);
        list.SelectionChanged+=(_,_)=>{if(loading)return;int next=list.SelectedIndex;try{Commit();Refresh(next);}catch(Exception e){status.Text=e.Message;loading=true;list.SelectedIndex=current;loading=false;}};
        Refresh(0);
    }
    private void Commit(){if(current>=0&&current<Result.Count)Result[current]=editor.Read();}
    private void Move(int offset){Commit();int next=current+offset;if(current<0||next<0||next>=Result.Count)return;(Result[current],Result[next])=(Result[next],Result[current]);Refresh(next);}
    private void Refresh(int selected)
    {
        loading=true;list.ItemsSource=Result.Select((a,i)=>$"{i+1}. {a.Name}\n{ActionRules.Describe(a)}").ToArray();current=selected>=0&&selected<Result.Count?selected:-1;list.SelectedIndex=current;editor.IsEnabled=current>=0;if(current>=0)editor.Load(config,Result[current],true);loading=false;
    }
}
