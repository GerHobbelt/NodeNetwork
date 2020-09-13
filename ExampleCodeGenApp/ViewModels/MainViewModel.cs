﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using DynamicData;
using ExampleCodeGenApp.Model;
using ExampleCodeGenApp.ViewModels.Nodes;
using NodeNetwork.Toolkit.Group;
using NodeNetwork.Toolkit.Layout;
using NodeNetwork.Toolkit.Layout.ForceDirected;
using NodeNetwork.Toolkit.NodeList;
using NodeNetwork.Utilities;
using NodeNetwork.ViewModels;
using ReactiveUI;

namespace ExampleCodeGenApp.ViewModels
{
    class NetworkBreadcrumb : BreadcrumbViewModel
    {
        #region Network
        private NetworkViewModel _network;
        public NetworkViewModel Network
        {
            get => _network;
            set => this.RaiseAndSetIfChanged(ref _network, value);
        }
        #endregion
    }

    public class MainViewModel : ReactiveObject
    {
        #region Network
        private ObservableAsPropertyHelper<NetworkViewModel> _network;
        public NetworkViewModel Network => _network.Value;
        #endregion

        public BreadcrumbBarViewModel NetworkBreadcrumbBar { get; } = new BreadcrumbBarViewModel();
        public NodeListViewModel NodeList { get; } = new NodeListViewModel();
        public CodePreviewViewModel CodePreview { get; } = new CodePreviewViewModel();
        public CodeSimViewModel CodeSim { get; } = new CodeSimViewModel();

        public ReactiveCommand<Unit, Unit> AutoLayout { get; }
		public ReactiveCommand<Unit, Unit> StartAutoLayoutLive { get; }
		public ReactiveCommand<Unit, Unit> StopAutoLayoutLive { get; }

        public ReactiveCommand<Unit, Unit> GroupNodes { get; }
        public ReactiveCommand<Unit, Unit> UngroupNodes { get; }
        public ReactiveCommand<Unit, Unit> OpenGroup { get; }

        public MainViewModel()
        {
            this.WhenAnyValue(vm => vm.NetworkBreadcrumbBar.ActiveItem).Cast<NetworkBreadcrumb>()
                .Select(b => b?.Network)
                .ToProperty(this, vm => vm.Network, out _network);
            NetworkBreadcrumbBar.ActivePath.Add(new NetworkBreadcrumb
            {
                Name = "Main",
                Network = new NetworkViewModel()
            });

            ButtonEventNode eventNode = new ButtonEventNode {CanBeRemovedByUser = false};
            Network.Nodes.Add(eventNode);

            //NodeList.AddNodeType(() => new ButtonEventNode());
            NodeList.AddNodeType(() => new ForLoopNode());
            NodeList.AddNodeType(() => new IntLiteralNode());
            NodeList.AddNodeType(() => new PrintNode());
            NodeList.AddNodeType(() => new TextLiteralNode());

            var codeObservable = eventNode.OnClickFlow.Values.Connect().Select(_ => new StatementSequence(eventNode.OnClickFlow.Values.Items));
            codeObservable.BindTo(this, vm => vm.CodePreview.Code);
            codeObservable.BindTo(this, vm => vm.CodeSim.Code);

			ForceDirectedLayouter layouter = new ForceDirectedLayouter();
			AutoLayout = ReactiveCommand.Create(() => layouter.Layout(new Configuration { Network = Network }, 10000));
			StartAutoLayoutLive = ReactiveCommand.CreateFromObservable(() => 
				Observable.StartAsync(ct => layouter.LayoutAsync(new Configuration { Network = Network }, ct)).TakeUntil(StopAutoLayoutLive)
			);
			StopAutoLayoutLive = ReactiveCommand.Create(() => { }, StartAutoLayoutLive.IsExecuting);

            var grouper = new NodeGrouper
            {
                GroupNodeFactory = subnet => new GroupNodeViewModel(subnet),
                EntranceNodeFactory = () => new NodeViewModel { Name = "Group Input" },
                ExitNodeFactory = () => new NodeViewModel { Name = "Group Output" },
                SubNetworkFactory = () => new NetworkViewModel(),
                IOBindingFactory = (groupNode, entranceNode, exitNode) =>
                    new CodeGroupIOBinding(groupNode, entranceNode, exitNode)
            };
            GroupNodes = ReactiveCommand.Create(() =>
            {
                var groupBinding = grouper.MergeIntoGroup(Network, Network.SelectedNodes.Items);
                ((GroupNodeViewModel) groupBinding.GroupNode).IOBinding = (CodeGroupIOBinding)groupBinding;
            }, this.WhenAnyObservable(vm => vm.Network.SelectedNodes.CountChanged).Select(c => c > 1));

            UngroupNodes = ReactiveCommand.Create(() =>
            {
                var selectedGroupNode = (GroupNodeViewModel)Network.SelectedNodes.Items.First();
                grouper.Ungroup(selectedGroupNode.IOBinding);
            }, this.WhenAnyValue(vm => vm.Network).Select(net => 
                    net.SelectedNodes.Connect().Select(_ =>
                    {
                        return net.SelectedNodes.Count == 1 && net.SelectedNodes.Items.First() is GroupNodeViewModel;
                    })
                ).Switch());

            OpenGroup = ReactiveCommand.Create(() =>
            {
                var node = (GroupNodeViewModel)Network.SelectedNodes.Items.First();
                NetworkBreadcrumbBar.ActivePath.Add(new NetworkBreadcrumb
                {
                    Network = node.Subnet,
                    Name = node.Name
                });
            }, this.WhenAnyValue(vm => vm.Network).Select(n => n.SelectedNodes.Connect()).Switch()
                .Select(_ => Network.SelectedNodes.Count == 1 && Network.SelectedNodes.Items.First() is GroupNodeViewModel)
            );
        }
    }
}
