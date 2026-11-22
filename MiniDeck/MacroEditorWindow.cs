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
        FontFamily=new System.Windows.Media.FontFamily("Segoe UI");FontSize=14;Ui.DarkTitleBar(this);
        var root=new DockPanel{Margin=new Thickness(22)};Content=root;
        var header=new StackPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        header.Children.Add(new TextBlock{Text="Montar sequência de ações",FontSize=22,FontWeight=FontWeights.SemiBold});
        header.Children.Add(new TextBlock{Text="Cada passo é uma ação, executada de cima para baixo. Clique num passo da lista para editá-lo à direita, use ↑ Subir / ↓ Descer para mudar a ordem e “Esperar” para dar pausas entre os passos. No fim, clique em Concluir sequência e depois em Salvar ação na janela principal.",Foreground=Ui.Brush("#93A3B8"),FontSize=12.5,LineHeight=19,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)});
        var bottom=new StackPanel{Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var actions=new WrapPanel();bottom.Children.Add(actions);bottom.Children.Add(status);
        Button Add(string name,string tip,Action click){var b=new Button{Content=name,ToolTip=tip};b.Click+=(_,_)=>{try{click();}catch(Exception e){status.Text=e.Message;}};actions.Children.Add(b);return b;}
        Add("+ Adicionar passo","Cria um passo novo no fim da lista (máximo de 100).",()=>{Commit();if(Result.Count>=100)throw new InvalidOperationException("Máximo de 100 passos.");Result.Add(new(){Name="Novo passo"});Refresh(Result.Count-1);});
        Add("↑ Subir","Move o passo selecionado uma posição para cima (ele roda antes).",()=>Move(-1));Add("↓ Descer","Move o passo selecionado uma posição para baixo (ele roda depois).",()=>Move(1));
        Add("Remover passo","Apaga o passo selecionado da sequência.",()=>{if(current<0)return;Result.RemoveAt(current);current=-1;Refresh(Math.Min(list.SelectedIndex,Result.Count-1));});
        Add("Salvar passo","Guarda as mudanças do passo selecionado na lista.",()=>{Commit();Refresh(current);status.Text="Passo salvo.";});
        Add("Concluir sequência","Fecha este editor e volta para a janela principal. Lá, clique em Salvar ação para guardar a sequência na tecla.",()=>{Commit();ActionRules.Validate(new(){Kind="sequence",Steps=Result});DialogResult=true;}).SetResourceReference(StyleProperty,"Primary");
        var grid=new Grid();grid.ColumnDefinitions.Add(new(){Width=new GridLength(250)});grid.ColumnDefinitions.Add(new());root.Children.Add(grid);
        var listPanel=new DockPanel{Margin=new Thickness(0,0,18,0)};var listTitle=new TextBlock{Text="PASSOS (na ordem de execução)",Foreground=Ui.Brush("#93A3B8"),FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(listTitle,Dock.Top);listPanel.Children.Add(listTitle);
        list.Margin=new Thickness(0);list.BorderBrush=Ui.Brush("#2A394E");list.Padding=new Thickness(4);list.ToolTip="Clique num passo para editá-lo à direita.";listPanel.Children.Add(list);grid.Children.Add(listPanel);
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
